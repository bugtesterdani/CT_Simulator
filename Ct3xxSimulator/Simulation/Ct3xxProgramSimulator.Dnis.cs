using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx DNIS small inductivity tests against the external DUT simulation.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunDnisTest.
    /// </summary>
    private TestOutcome RunDnisTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DNIS ohne Parameter.");
            return TestOutcome.Error;
        }

        var interfaceDefinition = ResolveDnisInterface(parameters);
        if (interfaceDefinition == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DNIS ohne definiertes SIND-Interface.");
            return TestOutcome.Error;
        }

        if (_wireVizResolver == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DNIS ohne geladene Verdrahtung.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DNIS ohne aktive Geraetesimulation.");
            return TestOutcome.Error;
        }

        var records = parameters.Tables
            .SelectMany(table => table.Records)
            .Where(record => string.Equals(record.Id, "SIND", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (records.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DNIS ohne SIND-Datensaetze.");
            return TestOutcome.Error;
        }

        var traces = BuildDnisTraces(interfaceDefinition);
        var overall = TestOutcome.Pass;
        foreach (var record in records)
        {
            if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(record.D, "yes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var evaluation = EvaluateDnisRecord(test, record, interfaceDefinition, traces);
            overall = CombineOutcomes(overall, evaluation.Outcome);
        }

        return overall;
    }

    private DnisRecordEvaluation EvaluateDnisRecord(Test test, Record record, InterfaceDefinition interfaceDefinition, IReadOnlyList<StepConnectionTrace> baseTraces)
    {
        var recordName = ResolveDnisName(GetRecordAttribute(record, "Name") ?? record.Text ?? record.TestPoint ?? $"SIND {record.Index}");
        var channelResolution = ResolveDnisChannel(recordName);
        if (channelResolution == null)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                details: $"{recordName}: Kanal nicht in der SIND-Signaltabelle gefunden.",
                stepNameOverride: recordName,
                traces: baseTraces);
            return new DnisRecordEvaluation(TestOutcome.Error);
        }

        var resolvedSignal = channelResolution.SignalName;
        var channelIndex = channelResolution.ChannelIndex;
        var expectedText = GetRecordAttribute(record, "Value");
        var toleranceText = GetRecordAttribute(record, "Tol") ?? GetRecordAttribute(record, "Tolerance");
        var serialText = GetRecordAttribute(record, "Imp") ??
                         GetRecordAttribute(record, "SerialConductance") ??
                         GetRecordAttribute(record, "SerialResistance");
        var compensationText = GetRecordAttribute(record, "Comp") ??
                               GetRecordAttribute(record, "Compensation");
        var writeVariable = record.DestinationVariable ??
                            GetRecordAttribute(record, "WriteToVariable") ??
                            GetRecordAttribute(record, "WriteVariable");

        if (!TryParseMeasurement(expectedText, out var expectedValue, out var expectedUnit))
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                details: $"{recordName}: erwarteter Wert fehlt.",
                stepNameOverride: recordName,
                traces: baseTraces);
            return new DnisRecordEvaluation(TestOutcome.Error);
        }

        var (lower, upper) = BuildToleranceLimits(expectedValue, toleranceText, null);
        if (!lower.HasValue && !upper.HasValue)
        {
            lower = expectedValue;
            upper = expectedValue;
        }

        double? serialConductance = null;
        string? serialUnit = null;
        if (TryParseMeasurement(serialText, out var serialValue, out var serialParsedUnit))
        {
            serialConductance = serialValue;
            serialUnit = serialParsedUnit;
        }

        double? compensation = null;
        if (TryParseMeasurement(compensationText, out var compensationValue, out _))
        {
            compensation = compensationValue;
        }

        var payload = BuildDnisPayload(recordName, resolvedSignal, channelIndex, expectedValue, expectedUnit, toleranceText, serialConductance, serialUnit, compensation, interfaceDefinition);
        if (!_externalDeviceSession!.TrySendInterface(interfaceDefinition.Name ?? "SIND", payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                details: $"{recordName}: Kommunikationsfehler ({error ?? "keine Antwort"}).",
                stepNameOverride: recordName,
                traces: baseTraces);
            return new DnisRecordEvaluation(TestOutcome.Error);
        }

        RefreshExternalDeviceState();
        if (!TryExtractDnisMeasurement(responsePayload, out var measuredValue, out var responseSerial, out var responseDetails, out var responseError))
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                details: $"{recordName}: Messung nicht moeglich ({responseError ?? "kein Messwert"}).",
                stepNameOverride: recordName,
                traces: baseTraces);
            return new DnisRecordEvaluation(TestOutcome.Error);
        }

        var seriesInductance = SumSeriesInductors(resolvedSignal);
        measuredValue += seriesInductance;

        var adjustedValue = compensation.HasValue ? measuredValue - compensation.Value : measuredValue;
        var outcome = IsWithinLimits(adjustedValue, lower, upper) ? TestOutcome.Pass : TestOutcome.Fail;

        if (!string.IsNullOrWhiteSpace(writeVariable))
        {
            TryWriteVariable(writeVariable!, adjustedValue);
        }

        RecordCurvePoint($"DNIS {recordName}", adjustedValue, expectedUnit);
        var channelTraces = CollectSignalTraces(resolvedSignal, "SIND Channel");
        var traces = baseTraces.Concat(channelTraces)
            .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        PublishStepEvaluation(
            test,
            outcome,
            measured: adjustedValue,
            lower: lower,
            upper: upper,
            unit: expectedUnit,
            details: BuildDnisDetails(recordName, channelIndex, expectedValue, expectedUnit, toleranceText, serialConductance, serialUnit, compensation, adjustedValue, responseSerial, responseDetails, seriesInductance),
            stepNameOverride: recordName,
            traces: traces,
            curvePoints: CaptureCurvePoints());
        return new DnisRecordEvaluation(outcome);
    }

    private InterfaceDefinition? ResolveDnisInterface(TestParameters parameters)
    {
        var interfaceName = NormalizeQuotedText(GetParameterAttribute(parameters, "Interface"));
        if (!string.IsNullOrWhiteSpace(interfaceName))
        {
            return FindInterfaceDefinition(interfaceName!);
        }

        if (_program == null)
        {
            return null;
        }

        var interfaces = _program.Tables.SelectMany(table => table.Interfaces).ToList();
        var byId = interfaces.FirstOrDefault(item => string.Equals(item.Id, "IND~", StringComparison.OrdinalIgnoreCase));
        if (byId != null)
        {
            return byId;
        }

        return interfaces.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Name) &&
            item.Name!.IndexOf("SIND", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private string ResolveDnisName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "SIND";
        }

        var trimmed = raw.Trim().Trim('\'', '"');
        if (trimmed.StartsWith("=", StringComparison.Ordinal))
        {
            return _evaluator.ResolveText(trimmed[1..].Trim());
        }

        return trimmed;
    }

    private int ParseRecordIndex(string? raw)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private IReadOnlyList<StepConnectionTrace> BuildDnisTraces(InterfaceDefinition definition)
    {
        var traces = new List<StepConnectionTrace>();
        foreach (var signal in new[]
                 {
                     ResolveI2cLine(definition.Sda),
                     ResolveI2cLine(definition.Scl),
                     ResolveI2cLine(definition.Ground),
                     ResolveI2cLine(GetInterfaceAttribute(definition, "Output"))
                 })
        {
            if (!string.IsNullOrWhiteSpace(signal))
            {
                traces.AddRange(CollectSignalTraces(signal!, "SIND"));
            }
        }

        if (traces.Count > 0)
        {
            return traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        var nodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.Module))
        {
            nodes.Add(definition.Module!);
        }

        if (!string.IsNullOrWhiteSpace(definition.Route))
        {
            nodes.Add(definition.Route!);
        }

        nodes.Add(definition.Name ?? "SIND");
        nodes.Add("SIND Module");
        return new[] { new StepConnectionTrace($"SIND {definition.Name ?? "DNIS"}", nodes) };
    }

    private static string? GetInterfaceAttribute(InterfaceDefinition definition, string name)
    {
        if (definition.AdditionalAttributes == null || definition.AdditionalAttributes.Length == 0)
        {
            return null;
        }

        return ReadAttributeValue(definition.AdditionalAttributes, name);
    }

    private void TryWriteVariable(string variableName, double value)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        var resolved = NormalizeQuotedText(_evaluator.ResolveText(variableName)) ?? variableName;
        var address = VariableAddress.From(resolved);
        _context.SetValue(address, value);
    }

    private DnisChannelResolution? ResolveDnisChannel(string recordName)
    {
        if (_fileSet == null)
        {
            return null;
        }

        var boardIndex = ResolveBoardIndex();
        var assignments = _fileSet.SignalTables
            .SelectMany(table => table.Table.Modules)
            .Where(module => string.Equals(module.Name, "SIND", StringComparison.OrdinalIgnoreCase))
            .SelectMany(module => module.Assignments)
            .Where(item => string.Equals(item.Name, recordName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (assignments.Count == 0)
        {
            return null;
        }

        if (boardIndex.HasValue)
        {
            var exact = assignments.FirstOrDefault(item => item.BoardNumber == boardIndex.Value);
            if (exact != null)
            {
                return new DnisChannelResolution(exact.Name, exact.Channel);
            }

            var fallback = assignments.FirstOrDefault(item => !item.BoardNumber.HasValue || item.BoardNumber.Value == 0);
            if (fallback != null)
            {
                return new DnisChannelResolution(fallback.Name, fallback.Channel);
            }
        }

        var first = assignments.FirstOrDefault();
        return first == null ? null : new DnisChannelResolution(first.Name, first.Channel);
    }

    private int? ResolveBoardIndex()
    {
        var fromContext = _evaluator.ToDouble(_context.GetValue(VariableAddress.From("$BoardIndex")));
        if (fromContext.HasValue && fromContext.Value > 0)
        {
            return (int)Math.Round(fromContext.Value, MidpointRounding.AwayFromZero);
        }

        var programText = _program?.BoardIndex?.Trim().Trim('\'', '"');
        if (int.TryParse(programText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private double SumSeriesInductors(string signalName)
    {
        if (_wireVizResolver == null)
        {
            return 0d;
        }

        if (!_wireVizResolver.TryTrace(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, out var traces))
        {
            return 0d;
        }

        var nodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trace in traces)
        {
            foreach (var node in trace.Nodes)
            {
                nodes.Add(node);
            }
        }

        var inductors = _wireVizResolver.SimulationElements
            .OfType<Ct3xxSimulationModelParser.Model.InductorElementDefinition>()
            .ToList();
        if (inductors.Count == 0)
        {
            return 0d;
        }

        var sum = 0d;
        foreach (var inductor in inductors)
        {
            if (nodes.Contains(inductor.A) && nodes.Contains(inductor.B))
            {
                sum += inductor.Henry;
            }
        }

        return sum;
    }

    private JsonObject BuildDnisPayload(
        string recordName,
        string signalName,
        int channelIndex,
        double expectedValue,
        string expectedUnit,
        string? toleranceText,
        double? serialConductance,
        string? serialUnit,
        double? compensation,
        InterfaceDefinition definition)
    {
        var boardIndex = ResolveBoardIndex()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return new JsonObject
        {
            ["protocol"] = "dnis",
            ["name"] = recordName,
            ["signal"] = signalName,
            ["channel_index"] = channelIndex,
            ["board_index"] = boardIndex,
            ["expected_inductance"] = expectedValue,
            ["expected_unit"] = expectedUnit,
            ["tolerance"] = toleranceText,
            ["serial_ohms"] = serialConductance,
            ["serial_unit"] = serialUnit,
            ["compensation"] = compensation,
            ["interface_name"] = NormalizeQuotedText(definition.Name),
            ["sda_signal"] = ResolveI2cLine(definition.Sda),
            ["scl_signal"] = ResolveI2cLine(definition.Scl),
            ["gnd_signal"] = ResolveI2cLine(definition.Ground),
            ["output_signal"] = ExtractSignalName(GetInterfaceAttribute(definition, "Output")),
            ["module"] = NormalizeQuotedText(definition.Module),
            ["route"] = NormalizeQuotedText(definition.Route)
        };
    }

    private static bool TryExtractDnisMeasurement(object? payload, out double value, out double? serial, out string details, out string? error)
    {
        value = 0d;
        serial = null;
        details = string.Empty;
        error = null;

        if (payload is JsonObject objectPayload)
        {
            if (objectPayload["error"] != null)
            {
                error = objectPayload["error"]?.ToString();
                return false;
            }

            if (objectPayload["measurements"] is JsonObject measurements)
            {
                if (TryReadNumeric(measurements["inductance"], out value))
                {
                    if (TryReadNumeric(measurements["serial_ohms"], out var serialValue) ||
                        TryReadNumeric(measurements["serial_conductance"], out serialValue) ||
                        TryReadNumeric(measurements["impedance"], out serialValue))
                    {
                        serial = serialValue;
                    }

                    details = BuildResponseDetails(objectPayload);
                    return true;
                }
            }

            if (TryReadNumeric(objectPayload["inductance"], out value) ||
                TryReadNumeric(objectPayload["value"], out value))
            {
                if (TryReadNumeric(objectPayload["serial_ohms"], out var serialValue) ||
                    TryReadNumeric(objectPayload["serial_conductance"], out serialValue) ||
                    TryReadNumeric(objectPayload["impedance"], out serialValue))
                {
                    serial = serialValue;
                }

                details = BuildResponseDetails(objectPayload);
                return true;
            }
        }

        if (payload is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var numeric))
        {
            value = numeric;
            return true;
        }

        if (payload is double number)
        {
            value = number;
            return true;
        }

        if (payload is float floatValue)
        {
            value = floatValue;
            return true;
        }

        if (payload is int intValue)
        {
            value = intValue;
            return true;
        }

        if (payload is long longValue)
        {
            value = longValue;
            return true;
        }

        error = "kein Messwert im DNIS-Response";
        return false;
    }
    private static string BuildDnisDetails(
        string recordName,
        int channelIndex,
        double expectedValue,
        string expectedUnit,
        string? toleranceText,
        double? serialConductance,
        string? serialUnit,
        double? compensation,
        double measuredValue,
        double? measuredSerial,
        string responseDetails,
        double seriesInductance)
    {
        var details = $"SIND {recordName} (CH {channelIndex}): erwartet {FormatMeasurement(expectedValue, expectedUnit)}";
        if (!string.IsNullOrWhiteSpace(toleranceText))
        {
            details += $" tol {toleranceText.Trim()}";
        }

        if (serialConductance.HasValue)
        {
            details += $", Rser {FormatMeasurement(serialConductance, serialUnit)}";
        }

        if (seriesInductance > 0)
        {
            details += $", Serie {FormatMeasurement(seriesInductance, expectedUnit)}";
        }

        if (compensation.HasValue)
        {
            details += $", Comp {FormatMeasurement(compensation, expectedUnit)}";
        }

        details += $", gemessen {FormatMeasurement(measuredValue, expectedUnit)}";
        if (measuredSerial.HasValue)
        {
            details += $", Rser(meas) {FormatMeasurement(measuredSerial, serialUnit ?? "Ohm")}";
        }

        if (!string.IsNullOrWhiteSpace(responseDetails))
        {
            details += responseDetails;
        }

        return details;
    }

    private sealed record DnisRecordEvaluation(TestOutcome Outcome);
    private sealed record DnisChannelResolution(string SignalName, int ChannelIndex);
}
