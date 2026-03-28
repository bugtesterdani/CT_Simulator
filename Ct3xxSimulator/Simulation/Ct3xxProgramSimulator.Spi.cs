using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx SPI test types with the tester acting as bus master and the external DUT acting as slave.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private TestOutcome RunSpiIoControlTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SPIX ohne Parameter.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-Geraetesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }

        var records = EnumerateSpiRecords(parameters).ToList();
        if (records.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SPIX ohne gueltige Datensaetze.");
            return TestOutcome.Error;
        }

        var details = new List<string>();
        var traces = new List<StepConnectionTrace>();
        var allCurvePoints = new List<MeasurementCurvePoint>();
        string? lastReadHex = null;
        var overall = TestOutcome.Pass;

        foreach (var record in records)
        {
            var interfaceDefinition = FindInterfaceDefinition(record.InterfaceName);
            if (interfaceDefinition == null)
            {
                overall = TestOutcome.Error;
                details.Add($"SPI-Interface nicht gefunden: {record.InterfaceName}");
                break;
            }

            var runtime = BuildSpiInterfaceRuntime(interfaceDefinition);
            ApplySpiSupply(runtime);
            traces.AddRange(BuildSpiTraces(runtime));

            var transaction = BuildSpiTransaction(record, runtime);
            var result = ExecuteSpiTransaction(runtime, record, transaction);
            details.Add(result.Details);
            allCurvePoints.AddRange(BuildSpiCurvePoints(runtime, transaction, result));

            if (!string.IsNullOrWhiteSpace(result.ReadHex))
            {
                lastReadHex = result.ReadHex;
                RecordCurvePoint($"SPI RX {record.Index}", ParseFirstByte(result.ReadHex), "byte");
            }

            if (!string.IsNullOrWhiteSpace(record.WriteVariable) && !string.IsNullOrWhiteSpace(result.ReadHex))
            {
                _context.SetValue(VariableAddress.From(record.WriteVariable), result.ReadHex);
            }

            if (result.Outcome != TestOutcome.Pass)
            {
                overall = result.Outcome;
                break;
            }

            if (record.WaitMs > 0)
            {
                AdvanceTime(record.WaitMs);
            }
        }

        PublishStepEvaluation(
            test,
            overall,
            measured: ParseFirstByte(lastReadHex),
            lower: 0,
            upper: 255,
            unit: "byte",
            details: string.Join(" | ", details),
            traces: traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            curvePoints: allCurvePoints);
        return overall;
    }

    private TestOutcome RunDm30Test(Test test)
    {
        var stepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "DM30";

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-Geraetesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }

        if (stepName.Equals("VCC ON", StringComparison.OrdinalIgnoreCase))
        {
            RememberSignal("VCC", 5.0d);
            PublishStepEvaluation(test, TestOutcome.Pass, measured: 5.0d, lower: 5.0d, upper: 5.0d, unit: "V", details: "DM30 Hilfsversorgung eingeschaltet.", traces: CollectSignalTraces("VCC", "SPI Versorgung"));
            return TestOutcome.Pass;
        }

        if (stepName.Equals("VCC OFF", StringComparison.OrdinalIgnoreCase))
        {
            RememberSignal("VCC", 0.0d);
            PublishStepEvaluation(test, TestOutcome.Pass, measured: 0.0d, lower: 0.0d, upper: 0.0d, unit: "V", details: "DM30 Hilfsversorgung ausgeschaltet.", traces: CollectSignalTraces("VCC", "SPI Versorgung"));
            return TestOutcome.Pass;
        }

        if (stepName.Equals("DM300 Write EEPROM", StringComparison.OrdinalIgnoreCase))
        {
            var serial = _evaluator.ToText(_context.GetValue(VariableAddress.From("Seriennummer")));
            if (string.IsNullOrWhiteSpace(serial))
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: "DM30 Write EEPROM ohne Seriennummer.");
                return TestOutcome.Error;
            }

            var writeSupplyVoltage = _signalState.TryGetValue("VCC", out var writeSupplyValue)
                ? _evaluator.ToDouble(writeSupplyValue)
                : 0d;

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["protocol"] = "spi",
                ["operation"] = "dm30_write_serial",
                ["tester_role"] = "master",
                ["external_device_role"] = "slave",
                ["interface_name"] = "DM30_SPI_EEPROM",
                ["serial_text"] = serial,
                ["power_source_voltage"] = writeSupplyVoltage,
            };

            if (!_externalDeviceSession.TrySendInterface("DM30_SPI_EEPROM", payload, _cancellationToken, out _, out var error, _simulatedTimeMs))
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Write EEPROM: {error}");
                return TestOutcome.Error;
            }

            RefreshExternalDeviceState();
            var traces = BuildNamedSpiTraces("MOSI", "MISO", "CLK", "CS", "VCC");
            var curvePoints = BuildSyntheticDm30SpiCurve(serial, writeOperation: true);
            PublishStepEvaluation(test, TestOutcome.Pass, details: $"DM30 EEPROM write: {serial}", traces: traces, curvePoints: curvePoints);
            return TestOutcome.Pass;
        }

        if (stepName.Equals("DM300 Read Complete EEPROM", StringComparison.OrdinalIgnoreCase))
        {
            var readSupplyVoltage = _signalState.TryGetValue("VCC", out var readSupplyValue)
                ? _evaluator.ToDouble(readSupplyValue)
                : 0d;

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["protocol"] = "spi",
                ["operation"] = "dm30_dump_hex",
                ["tester_role"] = "master",
                ["external_device_role"] = "slave",
                ["interface_name"] = "DM30_SPI_EEPROM",
                ["power_source_voltage"] = readSupplyVoltage,
            };

            if (!_externalDeviceSession.TrySendInterface("DM30_SPI_EEPROM", payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Read EEPROM: {error}");
                return TestOutcome.Error;
            }

            RefreshExternalDeviceState();
            var response = responsePayload as JsonObject;
            if (response?["status"]?.GetValue<string?>()?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
            {
                var detail = response["details"]?.GetValue<string?>() ?? "DM30 Read EEPROM fehlgeschlagen.";
                PublishStepEvaluation(test, TestOutcome.Error, details: detail);
                return TestOutcome.Error;
            }

            var readHex = response?["read_hex"]?.GetValue<string?>() ?? string.Empty;
            _context.SetValue(VariableAddress.From("DMReadDataString"), readHex);

            var traces = BuildNamedSpiTraces("MOSI", "MISO", "CLK", "CS", "VCC");
            var curvePoints = BuildSyntheticDm30SpiCurve(readHex, writeOperation: false);
            PublishStepEvaluation(test, TestOutcome.Pass, details: $"DM30 EEPROM read: {readHex}", traces: traces, curvePoints: curvePoints);
            return TestOutcome.Pass;
        }

        return RunGenericTest(test);
    }

    private SpiTransactionResult ExecuteSpiTransaction(SpiInterfaceRuntime runtime, SpiRecordDefinition record, SpiTransaction transaction)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocol"] = "spi",
            ["operation"] = "transaction",
            ["tester_role"] = "master",
            ["external_device_role"] = "slave",
            ["interface_name"] = runtime.Name,
            ["function"] = runtime.Function,
            ["frequency_hz"] = runtime.FrequencyHz,
            ["clock_phase"] = runtime.ClockPhase,
            ["clock_polarity"] = runtime.ClockPolarity,
            ["chip_select_active"] = runtime.ChipSelectActive,
            ["power_source_voltage"] = runtime.SupplyVoltage,
            ["power_source_signal"] = runtime.SupplySignal,
            ["line_cs"] = runtime.ChipSelectSignal,
            ["line_clk"] = runtime.ClockSignal,
            ["line_mosi"] = runtime.MosiSignal,
            ["line_miso"] = runtime.MisoSignal,
            ["line_gnd"] = runtime.GroundSignal,
            ["bit_count"] = transaction.BitCount,
            ["tx_hex"] = transaction.TxHex,
            ["wait_ms"] = record.WaitMs,
        };

        if (!_externalDeviceSession!.TrySendInterface(runtime.Name, payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            return new SpiTransactionResult(TestOutcome.Error, $"SPI {runtime.Name} [{record.Index}]: Kommunikationsfehler: {error}", null);
        }

        RefreshExternalDeviceState();
        var response = responsePayload as JsonObject;
        if (response == null)
        {
            return new SpiTransactionResult(TestOutcome.Error, $"SPI {runtime.Name} [{record.Index}]: Ungueltige Antwort.", null);
        }

        var status = response["status"]?.GetValue<string?>() ?? "ok";
        var readHex = NormalizeSpiHex(response["rx_hex"]?.GetValue<string?>());
        var detailText = response["details"]?.GetValue<string?>() ?? string.Empty;

        if (status.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            return new SpiTransactionResult(TestOutcome.Error, $"SPI {runtime.Name} [{record.Index}]: {detailText}", readHex);
        }

        if (!string.IsNullOrWhiteSpace(record.ExpectedHex) && !string.Equals(readHex, record.ExpectedHex, StringComparison.OrdinalIgnoreCase))
        {
            return new SpiTransactionResult(
                TestOutcome.Fail,
                $"SPI {runtime.Name} [{record.Index}]: erwartet {record.ExpectedHex}, erhalten {readHex ?? "<leer>"}",
                readHex);
        }

        var summary = $"SPI {runtime.Name} [{record.Index}] TX={transaction.TxHex}";
        if (!string.IsNullOrWhiteSpace(readHex))
        {
            summary += $" RX={readHex}";
        }

        if (!string.IsNullOrWhiteSpace(detailText))
        {
            summary += $" ({detailText})";
        }

        _observer.OnMessage(summary);
        return new SpiTransactionResult(TestOutcome.Pass, summary, readHex);
    }

    private IEnumerable<SpiRecordDefinition> EnumerateSpiRecords(TestParameters parameters)
    {
        foreach (var record in parameters.Records)
        {
            if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var interfaceName = ResolveInterfaceReference(GetSpiRecordAttribute(record, "Interface"));
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                continue;
            }

            var txHex = NormalizeSpiHex(EvaluateText(GetSpiRecordAttribute(record, "HexData")));
            var bitCount = ParseBitCount(EvaluateText(GetSpiRecordAttribute(record, "BitCount")), txHex);
            var expectedHex = NormalizeSpiHex(EvaluateText(GetSpiRecordAttribute(record, "Nom")));
            var waitMs = ParseDurationMilliseconds(EvaluateText(GetSpiRecordAttribute(record, "WaitTime")));
            var writeVariable = NormalizeQuotedText(EvaluateText(GetSpiRecordAttribute(record, "WriteVar")));
            var outputState = NormalizeQuotedText(EvaluateText(GetSpiRecordAttribute(record, "OutputState")));
            var lineComment = NormalizeQuotedText(EvaluateText(GetSpiRecordAttribute(record, "LineComment")));
            var index = int.TryParse(record.Index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex : 0;

            yield return new SpiRecordDefinition(index, interfaceName!, txHex ?? string.Empty, bitCount, expectedHex, waitMs, writeVariable, outputState, lineComment);
        }
    }

    private string? EvaluateText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        var decoded = WebUtility.HtmlDecode(raw);
        var concatenationParts = SplitSpiTextTopLevel(decoded, '&');
        if (concatenationParts.Count > 1)
        {
            var builder = new StringBuilder();
            foreach (var part in concatenationParts)
            {
                builder.Append(EvaluateTextPart(part));
            }

            return builder.ToString();
        }

        return EvaluateTextPart(decoded);
    }

    private static string? GetSpiRecordAttribute(Record record, string attributeName)
    {
        if (record.AdditionalAttributes == null || string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        var attribute = record.AdditionalAttributes.FirstOrDefault(item =>
            string.Equals(item.Name, attributeName, StringComparison.OrdinalIgnoreCase));
        return attribute == null ? null : WebUtility.HtmlDecode(attribute.Value).Trim();
    }

    private string EvaluateTextPart(string raw)
    {
        var trimmed = raw.Trim();
        var evaluated = _evaluator.Evaluate(trimmed);
        if (evaluated != null)
        {
            return _evaluator.ToText(evaluated);
        }

        if (VariableAddress.TryParse(trimmed, out var address))
        {
            return _evaluator.ToText(_context.GetValue(address));
        }

        return _evaluator.ToText(trimmed);
    }

    private static List<string> SplitSpiTextTopLevel(string text, char separator)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var start = 0;
        var depth = 0;
        var inSingleQuotes = false;
        var inDoubleQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }

            if (ch == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                continue;
            }

            if (inSingleQuotes || inDoubleQuotes)
            {
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                continue;
            }

            if (ch != separator || depth != 0)
            {
                continue;
            }

            var part = text[start..index].Trim();
            if (part.Length > 0)
            {
                result.Add(part);
            }

            start = index + 1;
        }

        var last = text[start..].Trim();
        if (last.Length > 0)
        {
            result.Add(last);
        }

        return result;
    }

    private SpiInterfaceRuntime BuildSpiInterfaceRuntime(InterfaceDefinition definition)
    {
        var name = NormalizeQuotedText(definition.Name) ?? "SPI";
        var route = NormalizeQuotedText(definition.Route);
        var function = NormalizeQuotedText(definition.Function) ?? "Master";
        var frequencyHz = ParseSpiFrequency(definition.Frequency);
        var clockPhase = NormalizeQuotedText(definition.ClockPhase) ?? "Rising edge";
        var clockPolarity = NormalizeQuotedText(definition.ClockPolarity) ?? "Low";
        var csActive = NormalizeQuotedText(definition.ChipSelectActive) ?? "Low";
        var uifSignal = ResolveSpiLine(definition.UifSignal);
        var cs = ResolveSpiLine(definition.ChipSelectSignal) ?? BuildDerivedSpiSignal(uifSignal, "CS");
        var clk = BuildDerivedSpiSignal(uifSignal, "CLK");
        var mosi = BuildDerivedSpiSignal(uifSignal, "MOSI");
        var miso = BuildDerivedSpiSignal(uifSignal, "MISO");
        var vcc = BuildDerivedSpiSignal(uifSignal, "VCC");
        var gnd = BuildDerivedSpiSignal(uifSignal, "GND");
        var supplyVoltage = ParseInterfaceSupplyVoltage(definition.PowerSource);

        return new SpiInterfaceRuntime(name, route, function, frequencyHz, clockPhase, clockPolarity, csActive, cs, clk, mosi, miso, vcc, gnd, supplyVoltage);
    }

    private void ApplySpiSupply(SpiInterfaceRuntime runtime)
    {
        if (!runtime.SupplyVoltage.HasValue)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(runtime.SupplySignal))
        {
            RememberSignal(runtime.SupplySignal!, runtime.SupplyVoltage.Value);
        }
    }

    private IReadOnlyList<StepConnectionTrace> BuildSpiTraces(SpiInterfaceRuntime runtime)
    {
        var traces = BuildNamedSpiTraces(runtime.MosiSignal, runtime.MisoSignal, runtime.ClockSignal, runtime.ChipSelectSignal, runtime.SupplySignal, runtime.GroundSignal);
        var summaryTrace = new StepConnectionTrace($"SPI {runtime.Name}", new[]
        {
            runtime.Function,
            runtime.Route ?? runtime.Name,
            "CS",
            "CLK",
            "MOSI",
            "MISO",
            "SPI DUT"
        });

        if (traces.Count == 0)
        {
            return new[] { summaryTrace };
        }

        traces.Add(summaryTrace);
        return traces
            .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private List<StepConnectionTrace> BuildNamedSpiTraces(params string?[] signals)
    {
        var traces = new List<StepConnectionTrace>();
        foreach (var signal in signals.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            traces.AddRange(CollectSignalTraces(signal!, "SPI"));
        }

        return traces
            .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private IReadOnlyList<MeasurementCurvePoint> BuildSpiCurvePoints(SpiInterfaceRuntime runtime, SpiTransaction transaction, SpiTransactionResult result)
    {
        var points = new List<MeasurementCurvePoint>();
        var time = _simulatedTimeMs;
        var halfPeriodMs = Math.Max(1.0, 500.0 / Math.Max(1.0, runtime.FrequencyHz));
        var bits = transaction.TxBits;
        var rxBits = ExpandHexToBits(result.ReadHex, bits.Count);

        points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} CS", runtime.ChipSelectActive.Equals("Low", StringComparison.OrdinalIgnoreCase) ? 1 : 0, "logic"));
        points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} CLK", runtime.ClockPolarity.Equals("High", StringComparison.OrdinalIgnoreCase) ? 1 : 0, "logic"));

        for (var index = 0; index < bits.Count; index++)
        {
            var txBit = bits[index];
            var rxBit = index < rxBits.Count ? rxBits[index] : 0;

            points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} MOSI", txBit, "logic"));
            points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} MISO", rxBit, "logic"));
            points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} CLK", runtime.ClockPolarity.Equals("High", StringComparison.OrdinalIgnoreCase) ? 1 : 0, "logic"));
            time += (long)Math.Round(halfPeriodMs, MidpointRounding.AwayFromZero);
            points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} CLK", runtime.ClockPolarity.Equals("High", StringComparison.OrdinalIgnoreCase) ? 0 : 1, "logic"));
            time += (long)Math.Round(halfPeriodMs, MidpointRounding.AwayFromZero);
        }

        points.Add(new MeasurementCurvePoint(time, $"SPI {runtime.Name} CS", runtime.ChipSelectActive.Equals("Low", StringComparison.OrdinalIgnoreCase) ? 0 : 1, "logic"));
        return points;
    }

    private IReadOnlyList<MeasurementCurvePoint> BuildSyntheticDm30SpiCurve(string value, bool writeOperation)
    {
        var normalized = NormalizeSpiHex(value) ?? string.Empty;
        var visibleBitCount = Math.Min(normalized.Length * 4, 256);
        var bits = ExpandHexToBits(normalized, visibleBitCount);
        var points = new List<MeasurementCurvePoint>();
        var time = _simulatedTimeMs;
        points.Add(new MeasurementCurvePoint(time, "SPI DM30 CS", 1, "logic"));
        for (var index = 0; index < bits.Count; index++)
        {
            var bit = bits[index];
            points.Add(new MeasurementCurvePoint(time, writeOperation ? "SPI DM30 MOSI" : "SPI DM30 MISO", bit, "logic"));
            points.Add(new MeasurementCurvePoint(time, "SPI DM30 CLK", 0, "logic"));
            time += 1;
            points.Add(new MeasurementCurvePoint(time, "SPI DM30 CLK", 1, "logic"));
            time += 1;
        }

        points.Add(new MeasurementCurvePoint(time, "SPI DM30 CS", 0, "logic"));
        return points;
    }

    private SpiTransaction BuildSpiTransaction(SpiRecordDefinition record, SpiInterfaceRuntime runtime)
    {
        var txHex = record.TxHex;
        if (string.IsNullOrWhiteSpace(txHex))
        {
            txHex = string.Empty;
        }

        var expectedNibbles = (int)Math.Ceiling(record.BitCount / 4.0);
        if (txHex.Length < expectedNibbles)
        {
            txHex = txHex.PadRight(expectedNibbles, '0');
        }
        else if (txHex.Length > expectedNibbles)
        {
            txHex = txHex[..expectedNibbles];
        }

        return new SpiTransaction(txHex, record.BitCount, ExpandHexToBits(txHex, record.BitCount));
    }

    private static List<int> ExpandHexToBits(string? hex, int bitCount)
    {
        var bits = new List<int>(Math.Max(bitCount, 0));
        var normalized = NormalizeSpiHex(hex) ?? string.Empty;
        foreach (var character in normalized)
        {
            var value = int.Parse(character.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            bits.Add((value >> 3) & 0x1);
            bits.Add((value >> 2) & 0x1);
            bits.Add((value >> 1) & 0x1);
            bits.Add(value & 0x1);
        }

        if (bits.Count > bitCount)
        {
            bits.RemoveRange(bitCount, bits.Count - bitCount);
        }
        else
        {
            while (bits.Count < bitCount)
            {
                bits.Add(0);
            }
        }

        return bits;
    }

    private static string? NormalizeSpiHex(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var character in raw.Trim().Trim('\'', '"'))
        {
            if (Uri.IsHexDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        if ((builder.Length % 2) != 0)
        {
            builder.Insert(0, '0');
        }

        return builder.ToString();
    }

    private static int ParseBitCount(string? raw, string? txHex)
    {
        if (int.TryParse(NormalizeQuotedText(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return Math.Max(0, (NormalizeSpiHex(txHex)?.Length ?? 0) * 4);
    }

    private string? ResolveInterfaceReference(string? raw)
    {
        var normalized = NormalizeQuotedText(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.StartsWith("\\", StringComparison.Ordinal))
        {
            normalized = _evaluator.ResolveText(normalized[1..]);
        }

        return NormalizeQuotedText(normalized);
    }

    private static double ParseSpiFrequency(string? raw)
    {
        var normalized = NormalizeQuotedText(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 1000.0d;
        }

        normalized = normalized.Replace("Hz", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var multiplier = 1.0d;
        if (normalized.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1000.0d;
            normalized = normalized[..^1].Trim();
        }
        else if (normalized.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1000_000.0d;
            normalized = normalized[..^1].Trim();
        }

        normalized = normalized.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed * multiplier
            : 1000.0d;
    }

    private string? ResolveSpiLine(string? rawSignal)
    {
        var signal = NormalizeQuotedText(rawSignal);
        if (string.IsNullOrWhiteSpace(signal) || signal == "?")
        {
            return null;
        }

        return ExtractSignalName(signal);
    }

    private static string? BuildDerivedSpiSignal(string? baseSignal, string suffix)
    {
        if (string.IsNullOrWhiteSpace(baseSignal))
        {
            return null;
        }

        return $"{baseSignal}.{suffix}";
    }

    private static double? ParseFirstByte(string? hex)
    {
        var normalized = NormalizeSpiHex(hex);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 2)
        {
            return null;
        }

        return byte.TryParse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private sealed record SpiInterfaceRuntime(
        string Name,
        string? Route,
        string Function,
        double FrequencyHz,
        string ClockPhase,
        string ClockPolarity,
        string ChipSelectActive,
        string? ChipSelectSignal,
        string? ClockSignal,
        string? MosiSignal,
        string? MisoSignal,
        string? SupplySignal,
        string? GroundSignal,
        double? SupplyVoltage);

    private sealed record SpiRecordDefinition(
        int Index,
        string InterfaceName,
        string TxHex,
        int BitCount,
        string? ExpectedHex,
        long WaitMs,
        string? WriteVariable,
        string? OutputState,
        string? LineComment);

    private sealed record SpiTransaction(string TxHex, int BitCount, IReadOnlyList<int> TxBits);

    private sealed record SpiTransactionResult(TestOutcome Outcome, string Details, string? ReadHex);
}
