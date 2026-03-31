using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx I2C test types with the tester acting as bus master and the external DUT acting as slave.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunI2cInterfaceTest.
    /// </summary>
    private TestOutcome RunI2cInterfaceTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "2C2I ohne Parameter.");
            return TestOutcome.Error;
        }

        var interfaceName = NormalizeQuotedText(GetParameterAttribute(parameters, "Interface"));
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "2C2I ohne Interface.");
            return TestOutcome.Error;
        }

        var interfaceDefinition = FindInterfaceDefinition(interfaceName!);
        if (interfaceDefinition == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"I2C-Interface nicht gefunden: {interfaceName}");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-Geraetesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }

        var commands = EnumerateI2cRecords(parameters).ToList();
        if (commands.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "2C2I ohne Datensaetze.");
            return TestOutcome.Error;
        }

        var runtime = BuildI2cInterfaceRuntime(interfaceDefinition);
        ApplyI2cSupply(runtime);

        var traces = BuildI2cTraces(runtime);
        var details = new List<string>();
        byte? lastByte = null;
        var outcome = TestOutcome.Pass;

        foreach (var command in commands)
        {
            var result = ExecuteI2cRecord(runtime, command);
            details.Add(result.Details);
            if (result.ActualByte.HasValue)
            {
                lastByte = result.ActualByte.Value;
                RecordCurvePoint($"I2C {command.Index}", result.ActualByte.Value, "byte");
            }

            if (result.Outcome != TestOutcome.Pass)
            {
                outcome = result.Outcome;
                break;
            }

            if (command.WaitMs > 0)
            {
                AdvanceTime(command.WaitMs);
            }
        }

        PublishStepEvaluation(
            test,
            outcome,
            measured: lastByte,
            lower: 0,
            upper: 255,
            unit: "byte",
            details: string.Join(" | ", details),
            traces: traces);
        return outcome;
    }

    /// <summary>
    /// Executes ExecuteI2cRecord.
    /// </summary>
    private I2cTransactionResult ExecuteI2cRecord(I2cInterfaceRuntime runtime, I2cRecordDefinition command)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocol"] = "i2c",
            ["operation"] = "transaction",
            ["tester_role"] = "master",
            ["external_device_role"] = "slave",
            ["interface_name"] = runtime.Name,
            ["route"] = runtime.Route,
            ["module"] = runtime.Module,
            ["transfer_phase"] = DetermineI2cTransferPhase(command.AckMode),
            ["start_condition"] = command.StartCondition,
            ["stop_condition"] = command.EndCondition,
            ["ack_mode"] = command.AckMode,
            ["to_send"] = command.ToSend,
            ["expected"] = command.Expected,
            ["mask"] = command.Mask,
            ["wait_ms"] = command.WaitMs,
            ["sda_signal"] = runtime.SdaSignal,
            ["scl_signal"] = runtime.SclSignal,
            ["gnd_signal"] = runtime.GroundSignal,
            ["supply_signal"] = runtime.SupplySignal,
            ["supply_voltage"] = runtime.SupplyVoltage,
            ["stimulus_signals"] = runtime.StimulusSignals,
            ["acquire_signals"] = runtime.AcquireSignals,
        };

        if (!_externalDeviceSession!.TrySendInterface(runtime.Name, payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            return new I2cTransactionResult(
                TestOutcome.Error,
                $"I2C {runtime.Name} [{command.Index}]: Kommunikationsfehler: {error}",
                null);
        }

        RefreshExternalDeviceState();
        var response = responsePayload as JsonObject;
        if (response == null)
        {
            return new I2cTransactionResult(
                TestOutcome.Error,
                $"I2C {runtime.Name} [{command.Index}]: Ungueltige Antwort.",
                null);
        }

        var acknowledged = response["acknowledged"]?.GetValue<bool?>() ?? false;
        var actualByte = ReadResponseByte(response, "actual_byte");
        var expectedByte = command.Expected.HasValue
            ? (actualByte.HasValue ? (byte?)(byte)(actualByte.Value & command.Mask) : null)
            : null;
        var expectedMasked = command.Expected.HasValue ? (byte)(command.Expected.Value & command.Mask) : (byte?)null;

        if (!acknowledged)
        {
            var nackText = response["details"]?.GetValue<string?>() ?? "I2C-Slave hat kein ACK geliefert.";
            return new I2cTransactionResult(
                TestOutcome.Error,
                $"I2C {runtime.Name} [{command.Index}]: {nackText}",
                actualByte);
        }

        if (expectedMasked.HasValue && expectedByte != expectedMasked)
        {
            return new I2cTransactionResult(
                TestOutcome.Fail,
                $"I2C {runtime.Name} [{command.Index}]: erwartet 0x{expectedMasked.Value:X2}, erhalten 0x{(actualByte ?? 0):X2} (Maske 0x{command.Mask:X2})",
                actualByte);
        }

        var detailText = response["details"]?.GetValue<string?>() ?? string.Empty;
        var summary = $"I2C {runtime.Name} [{command.Index}] {BuildI2cOperationSummary(command)}";
        if (actualByte.HasValue)
        {
            summary += $" -> 0x{actualByte.Value:X2}";
        }

        if (!string.IsNullOrWhiteSpace(detailText))
        {
            summary += $" ({detailText})";
        }

        _observer.OnMessage(summary);
        return new I2cTransactionResult(TestOutcome.Pass, summary, actualByte);
    }

    /// <summary>
    /// Executes DetermineI2cTransferPhase.
    /// </summary>
    private static string DetermineI2cTransferPhase(string ackMode)
    {
        var normalized = NormalizeQuotedText(ackMode) ?? string.Empty;
        if (normalized.Equals("Write", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("No Ack", StringComparison.OrdinalIgnoreCase))
        {
            return "master_read";
        }

        return "master_write";
    }

    /// <summary>
    /// Executes BuildI2cOperationSummary.
    /// </summary>
    private static string BuildI2cOperationSummary(I2cRecordDefinition command)
    {
        var phase = DetermineI2cTransferPhase(command.AckMode);
        var normalizedAck = NormalizeQuotedText(command.AckMode) ?? "Read";
        if (phase == "master_read")
        {
            return $"Master RX ({normalizedAck}): 0x{command.ToSend:X2}";
        }

        return $"Master TX ({normalizedAck}): 0x{command.ToSend:X2}";
    }

    /// <summary>
    /// Executes FindInterfaceDefinition.
    /// </summary>
    private InterfaceDefinition? FindInterfaceDefinition(string interfaceName)
    {
        if (_program == null)
        {
            return null;
        }

        return _program.Tables
            .SelectMany(table => table.Interfaces)
            .FirstOrDefault(item => string.Equals(item.Name, interfaceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Executes EnumerateI2cRecords.
    /// </summary>
    private IEnumerable<I2cRecordDefinition> EnumerateI2cRecords(TestParameters parameters)
    {
        foreach (var record in parameters.Records)
        {
            if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var startCondition = ParseOnOffFlag(GetRecordAttribute(record, "StartCond"));
            var endCondition = ParseOnOffFlag(GetRecordAttribute(record, "EndCond"));
            var ackMode = NormalizeQuotedText(GetRecordAttribute(record, "Ack")) ?? "READ";
            var toSend = ParseByteValue(GetRecordAttribute(record, "ToSend"));
            var expected = ParseOptionalByteValue(GetRecordAttribute(record, "Expected"));
            var mask = ParseOptionalByteValue(GetRecordAttribute(record, "Mask")) ?? 0xFF;
            var waitMs = ParseDurationMilliseconds(GetRecordAttribute(record, "Wait"));
            var rowComment = NormalizeQuotedText(GetRecordAttribute(record, "RowComment"));
            var index = int.TryParse(record.Index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex : 0;

            yield return new I2cRecordDefinition(
                index,
                startCondition,
                endCondition,
                ackMode,
                toSend,
                expected,
                mask,
                waitMs,
                rowComment);
        }
    }

    /// <summary>
    /// Executes BuildI2cInterfaceRuntime.
    /// </summary>
    private I2cInterfaceRuntime BuildI2cInterfaceRuntime(InterfaceDefinition definition)
    {
        var name = NormalizeQuotedText(definition.Name) ?? "I2C";
        var route = NormalizeQuotedText(definition.Route);
        var module = NormalizeQuotedText(definition.Module);
        var sda = ResolveI2cLine(definition.Sda);
        var scl = ResolveI2cLine(definition.Scl);
        var gnd = ResolveI2cLine(definition.Ground);
        var supply = ResolveI2cLine(definition.SupplyChannel);
        var supplyVoltage = ParseInterfaceSupplyVoltage(definition.Supply);
        var stimulusSignals = new[]
        {
            ResolveI2cLine(definition.SdaStimulus),
            ResolveI2cLine(definition.SclStimulus),
        }.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var acquireSignals = new[]
        {
            ResolveI2cLine(definition.SdaAcquire),
            ResolveI2cLine(definition.SclAcquire),
        }.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return new I2cInterfaceRuntime(name, route, module, sda, scl, gnd, supply, supplyVoltage, stimulusSignals, acquireSignals);
    }

    /// <summary>
    /// Executes ApplyI2cSupply.
    /// </summary>
    private void ApplyI2cSupply(I2cInterfaceRuntime runtime)
    {
        if (!runtime.SupplyVoltage.HasValue)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(runtime.SupplySignal))
        {
            WriteSignal(runtime.SupplySignal!, runtime.SupplyVoltage.Value);
        }
        else
        {
            RememberSignal($"{runtime.Name}.VOUT", runtime.SupplyVoltage.Value);
        }
    }

    /// <summary>
    /// Executes BuildI2cTraces.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace> BuildI2cTraces(I2cInterfaceRuntime runtime)
    {
        var traces = new List<StepConnectionTrace>();
        foreach (var signal in new[] { runtime.SdaSignal, runtime.SclSignal, runtime.GroundSignal, runtime.SupplySignal }
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            traces.AddRange(CollectSignalTraces(signal!, $"I2C {runtime.Name}"));
        }

        foreach (var signal in runtime.StimulusSignals)
        {
            traces.AddRange(CollectSignalTraces(signal, $"I2C {runtime.Name} Stimulus"));
        }

        foreach (var signal in runtime.AcquireSignals)
        {
            traces.AddRange(CollectSignalTraces(signal, $"I2C {runtime.Name} Acquire"));
        }

        if (traces.Count > 0)
        {
            return traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        var nodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(runtime.Module))
        {
            nodes.Add(runtime.Module!);
        }

        if (!string.IsNullOrWhiteSpace(runtime.Route))
        {
            nodes.Add(runtime.Route!);
        }

        nodes.Add(runtime.Name);
        nodes.Add("I2C DUT");
        return new[] { new StepConnectionTrace($"I2C {runtime.Name}", nodes) };
    }

    /// <summary>
    /// Executes ResolveI2cLine.
    /// </summary>
    private string? ResolveI2cLine(string? rawSignal)
    {
        var signal = NormalizeQuotedText(rawSignal);
        if (string.IsNullOrWhiteSpace(signal) || signal == "?")
        {
            return null;
        }

        if (signal.StartsWith("MBUS", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveMeasurementBusSignal(signal) ?? signal;
        }

        return ExtractSignalName(signal);
    }

    /// <summary>
    /// Executes NormalizeQuotedText.
    /// </summary>
    private static string? NormalizeQuotedText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('\'', '"');

    /// <summary>
    /// Executes ParseOnOffFlag.
    /// </summary>
    private static bool ParseOnOffFlag(string? value) =>
        string.Equals(NormalizeQuotedText(value), "On", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes ParseByteValue.
    /// </summary>
    private static byte ParseByteValue(string? raw)
    {
        var parsed = ParseOptionalByteValue(raw);
        return parsed ?? 0;
    }

    /// <summary>
    /// Executes ParseOptionalByteValue.
    /// </summary>
    private static byte? ParseOptionalByteValue(string? raw)
    {
        var normalized = NormalizeQuotedText(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        normalized = normalized.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Executes ParseInterfaceSupplyVoltage.
    /// </summary>
    private static double? ParseInterfaceSupplyVoltage(string? raw)
    {
        var normalized = NormalizeQuotedText(raw);
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        normalized = normalized.Replace("V", string.Empty, StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("P", ".", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Executes ReadResponseByte.
    /// </summary>
    private static byte? ReadResponseByte(JsonObject response, string propertyName)
    {
        if (response[propertyName] is JsonValue valueNode)
        {
            if (valueNode.TryGetValue<int>(out var intValue))
            {
                return (byte)intValue;
            }

            if (valueNode.TryGetValue<long>(out var longValue))
            {
                return (byte)longValue;
            }

            if (valueNode.TryGetValue<string>(out var stringValue))
            {
                return ParseOptionalByteValue(stringValue);
            }
        }

        return null;
    }

    /// <summary>
    /// Executes I2cInterfaceRuntime.
    /// </summary>
    private sealed record I2cInterfaceRuntime(
        string Name,
        string? Route,
        string? Module,
        string? SdaSignal,
        string? SclSignal,
        string? GroundSignal,
        string? SupplySignal,
        double? SupplyVoltage,
        IReadOnlyList<string> StimulusSignals,
        IReadOnlyList<string> AcquireSignals);

    /// <summary>
    /// Executes I2cRecordDefinition.
    /// </summary>
    private sealed record I2cRecordDefinition(
        int Index,
        bool StartCondition,
        bool EndCondition,
        string AckMode,
        byte ToSend,
        byte? Expected,
        byte Mask,
        long WaitMs,
        string? RowComment);

    /// <summary>
    /// Executes I2cTransactionResult.
    /// </summary>
    private sealed record I2cTransactionResult(
        TestOutcome Outcome,
        string Details,
        byte? ActualByte);
}
