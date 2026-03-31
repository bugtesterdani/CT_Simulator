using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes AM2A analog tests based on .ctalg definitions.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private static readonly Encoding Am2aEncoding = GetAm2aEncoding();

    /// <summary>
    /// Executes RunAm2aTest.
    /// </summary>
    private TestOutcome RunAm2aTest(Test test)
    {
        if (_fileSet == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "AM2A ohne geladenes Programm.");
            return TestOutcome.Error;
        }

        if (!TryLoadAm2aDocument(test, out var document, out var error))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: error ?? "AM2A-Datei konnte nicht geladen werden.");
            return TestOutcome.Error;
        }

        var definitions = ParseAm2aDocument(document);
        if (definitions.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "AM2A enthaelt keine Parameter.");
            return TestOutcome.Error;
        }

        var overall = TestOutcome.Pass;
        foreach (var definition in definitions)
        {
            var outcome = ExecuteAm2aDefinition(test, definition, out var details);
            PublishStepEvaluation(
                test,
                outcome,
                details: details,
                traces: BuildAm2aTraces(definition),
                stepNameOverride: definition.Name ?? test.Name ?? test.Id ?? "AM2A");
            overall = CombineOutcomes(overall, outcome);
        }

        return overall;
    }

    private TestOutcome ExecuteAm2aDefinition(Test test, Am2aDefinition definition, out string details)
    {
        var messages = new List<string>();
        var outcome = TestOutcome.Pass;

        if (definition.Scanner != null || definition.Wires != null || definition.Guard1 != null)
        {
            messages.Add("AM2A: Scanner/Wires/Guard1 sind noch nicht funktional umgesetzt.");
            _observer.OnMessage("AM2A Hinweis: Scanner/Wires/Guard1 werden nur dokumentiert (siehe EXTENSIONS.md).");
        }

        if (definition.Filter50 != null)
        {
            messages.Add("AM2A: Filter50 wird aktuell nicht simuliert.");
        }

        if (definition.SeriesResistor.HasValue)
        {
            messages.Add($"AM2A: SeriesResistor {definition.SeriesResistor.Value.ToString("0.###", CultureInfo.InvariantCulture)} Ohm wird aktuell nicht simuliert.");
        }

        var stimulusSignal = definition.NetStimOut.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(stimulusSignal))
        {
            if (!string.Equals(definition.StimuliType, "off", StringComparison.OrdinalIgnoreCase))
            {
                if (definition.StimuliType.Contains("voltage", StringComparison.OrdinalIgnoreCase) && definition.StimuliVoltage.HasValue)
                {
                    if (definition.VoltageLimit.HasValue && Math.Abs(definition.StimuliVoltage.Value) > definition.VoltageLimit.Value)
                    {
                        outcome = CombineOutcomes(outcome, TestOutcome.Error);
                        messages.Add($"Stimulus {stimulusSignal}: VoltageLimit {definition.VoltageLimit.Value} V ueberschritten.");
                    }

                    WriteSignal(stimulusSignal, definition.StimuliVoltage.Value);
                    RecordCurvePoint($"AM2A Stimulus {stimulusSignal}", definition.StimuliVoltage.Value, "V");
                    messages.Add($"Stimulus {stimulusSignal} <= {definition.StimuliVoltage.Value.ToString("0.###", CultureInfo.InvariantCulture)} V");
                }
                else if (definition.StimuliType.Contains("current", StringComparison.OrdinalIgnoreCase) && definition.StimuliCurrent.HasValue)
                {
                    if (definition.CurrentLimit.HasValue && Math.Abs(definition.StimuliCurrent.Value) > definition.CurrentLimit.Value)
                    {
                        outcome = CombineOutcomes(outcome, TestOutcome.Error);
                        messages.Add($"Stimulus {stimulusSignal}: CurrentLimit {definition.CurrentLimit.Value} A ueberschritten.");
                    }

                    WriteSignal(stimulusSignal, definition.StimuliCurrent.Value);
                    RecordCurvePoint($"AM2A Stimulus {stimulusSignal}", definition.StimuliCurrent.Value, "A");
                    messages.Add($"Stimulus {stimulusSignal} <= {definition.StimuliCurrent.Value.ToString("0.###", CultureInfo.InvariantCulture)} A");
                }
                else
                {
                    messages.Add($"StimulusType '{definition.StimuliType}' ohne passende Stimuluswerte.");
                }
            }
        }

        if (definition.MeasureDelayMs > 0)
        {
            AdvanceTime(definition.MeasureDelayMs);
        }

        var acquisitionSignal = definition.NetStimSense.FirstOrDefault() ?? definition.NetStimOut.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(acquisitionSignal))
        {
            messages.Add("AM2A: Kein Acquisition-Signal (Net.Stim.Sense/Net.Stim.Out) definiert.");
            details = string.Join(" | ", messages);
            return CombineOutcomes(outcome, TestOutcome.Error);
        }

        var measurement = TryReadSignal(acquisitionSignal, out var rawValue, out var readDetails)
            ? _evaluator.ToDouble(rawValue)
            : null;

        if (!measurement.HasValue)
        {
            messages.Add($"AM2A: Messwert fuer {acquisitionSignal} fehlt ({readDetails}).");
            details = string.Join(" | ", messages);
            return CombineOutcomes(outcome, TestOutcome.Error);
        }

        var voltageOutcome = EvaluateLimitSet(
            "Voltage",
            measurement.Value,
            definition.LowerVoltageBound,
            definition.UpperVoltageBound,
            "V",
            messages);

        var currentOutcome = EvaluateLimitSet(
            "Current",
            measurement.Value,
            definition.LowerCurrentBound,
            definition.UpperCurrentBound,
            "A",
            messages);

        var voltageRangeOutcome = EvaluateRangeSet(
            "VoltageRange",
            measurement.Value,
            definition.VoltageMeasuringRange,
            "V",
            messages);

        var currentRangeOutcome = EvaluateRangeSet(
            "CurrentRange",
            measurement.Value,
            definition.CurrentMeasuringRange,
            "A",
            messages);

        outcome = CombineOutcomes(outcome, voltageOutcome);
        outcome = CombineOutcomes(outcome, currentOutcome);
        outcome = CombineOutcomes(outcome, voltageRangeOutcome);
        outcome = CombineOutcomes(outcome, currentRangeOutcome);

        details = string.Join(" | ", messages);
        return outcome;
    }

    private TestOutcome EvaluateLimitSet(string label, double measured, double? lower, double? upper, string unit, List<string> messages)
    {
        if (!lower.HasValue && !upper.HasValue)
        {
            return TestOutcome.Pass;
        }

        var outcome = EvaluateNumericOutcome(measured, lower, upper);
        var limitText = $"{FormatLimit(lower)} .. {FormatLimit(upper)} {unit}";
        messages.Add($"{label}: {measured.ToString("0.###", CultureInfo.InvariantCulture)} {unit} (Limits {limitText}) => {outcome.ToString().ToUpperInvariant()}");
        return outcome;
    }

    private TestOutcome EvaluateRangeSet(string label, double measured, double? range, string unit, List<string> messages)
    {
        if (!range.HasValue)
        {
            return TestOutcome.Pass;
        }

        var within = Math.Abs(measured) <= range.Value;
        var outcome = within ? TestOutcome.Pass : TestOutcome.Error;
        messages.Add($"{label}: {measured.ToString("0.###", CultureInfo.InvariantCulture)} {unit} (Range ±{range.Value.ToString("0.###", CultureInfo.InvariantCulture)} {unit}) => {outcome.ToString().ToUpperInvariant()}");
        return outcome;
    }

    private IReadOnlyList<StepConnectionTrace> BuildAm2aTraces(Am2aDefinition definition)
    {
        if (_wireVizResolver == null)
        {
            return Array.Empty<StepConnectionTrace>();
        }

        var traces = new List<StepConnectionTrace>();
        foreach (var net in definition.NetStimOut.Concat(definition.NetStimSense).Concat(definition.NetGndOut).Concat(definition.NetGndSense))
        {
            traces.AddRange(CollectSignalTraces(net, "AM2A"));
        }

        return traces
            .DistinctBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryLoadAm2aDocument(Test test, out AlgorithmDocument document, out string? error)
    {
        var fileName = test.File?.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(fileName))
        {
            document = null!;
            error = "AM2A ohne referenzierte .ctalg-Datei.";
            return false;
        }

        if (_fileSet != null)
        {
            var match = _fileSet.GetDocuments<AlgorithmDocument>()
                .FirstOrDefault(item => string.Equals(item.FileName, Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                document = match;
                error = null;
                return true;
            }
        }

        var resolved = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(_fileSet!.ProgramDirectory, fileName);
        if (!File.Exists(resolved))
        {
            document = null!;
            error = $"AM2A-Datei nicht gefunden: {resolved}";
            return false;
        }

        var lines = File.ReadAllLines(resolved, Am2aEncoding);
        document = new AlgorithmDocument(resolved, null, lines);
        error = null;
        return true;
    }

    private static List<Am2aDefinition> ParseAm2aDocument(AlgorithmDocument document)
    {
        var result = new List<Am2aDefinition>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var insideTest = false;
        var insideParameters = false;
        string? typeId = null;

        foreach (var rawLine in document.Lines)
        {
            var line = StripComments(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("test", StringComparison.OrdinalIgnoreCase))
            {
                insideTest = true;
                continue;
            }

            if (insideTest && trimmed.StartsWith("typeid", StringComparison.OrdinalIgnoreCase))
            {
                var (_, value) = ParseKeyValue(trimmed);
                typeId = value;
                continue;
            }

            if (trimmed.StartsWith("parameters", StringComparison.OrdinalIgnoreCase))
            {
                insideParameters = true;
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (trimmed == "}" && insideParameters)
            {
                if (current.Count > 0)
                {
                    result.Add(Am2aDefinition.FromParameters(current, typeId));
                }

                insideParameters = false;
                continue;
            }

            if (trimmed == "}" && insideTest)
            {
                insideTest = false;
                typeId = null;
                continue;
            }

            if (insideParameters)
            {
                var (key, value) = ParseKeyValue(trimmed);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    current[key!] = value;
                }
            }
        }

        return result;
    }

    private sealed class Am2aDefinition
    {
        private Am2aDefinition(Dictionary<string, string> parameters, string? typeId)
        {
            Parameters = parameters;
            TypeId = typeId ?? string.Empty;
            Name = GetParameter("Name") ?? GetParameter("DrawingReference");
            StimuliType = GetParameter("StimuliType") ?? "off";
            StimuliVoltage = ParseMeasurement(GetParameter("StimuliVoltage"), "V");
            StimuliCurrent = ParseMeasurement(GetParameter("StimuliCurrent"), "A");
            VoltageLimit = ParseMeasurement(GetParameter("VoltageLimit"), "V");
            CurrentLimit = ParseMeasurement(GetParameter("CurrentLimit"), "A");
            SeriesResistor = ParseMeasurement(GetParameter("SeriesResistor"), "Ohm");
            AcquisitionType = GetParameter("AcquistionType") ?? GetParameter("AcquisitionType") ?? "voltage";
            UpperVoltageBound = ParseMeasurement(GetParameter("UpperVoltageBound"), "V");
            LowerVoltageBound = ParseMeasurement(GetParameter("LowerVoltageBound"), "V");
            UpperCurrentBound = ParseMeasurement(GetParameter("UpperCurrentBound"), "A");
            LowerCurrentBound = ParseMeasurement(GetParameter("LowerCurrentBound"), "A");
            VoltageMeasuringRange = ParseMeasurement(GetParameter("VoltageMeasuringRange"), "V");
            CurrentMeasuringRange = ParseMeasurement(GetParameter("CurrentMeasuringRange"), "A");
            MeasureDelayMs = ParseDurationMilliseconds(GetParameter("MeasDelay"));
            Filter50 = GetParameter("Filter50");
            Scanner = GetParameter("Scanner");
            Wires = GetParameter("Wires");
            Guard1 = GetParameter("Guard1");
            NetStimOut = SplitNets(GetParameter("Net.Stim.Out"));
            NetStimSense = SplitNets(GetParameter("Net.Stim.Sense"));
            NetGndOut = SplitNets(GetParameter("Net.Gnd.Out"));
            NetGndSense = SplitNets(GetParameter("Net.Gnd.Sense"));
        }

        public string TypeId { get; }
        public string? Name { get; }
        public string StimuliType { get; }
        public double? StimuliVoltage { get; }
        public double? StimuliCurrent { get; }
        public double? VoltageLimit { get; }
        public double? CurrentLimit { get; }
        public double? SeriesResistor { get; }
        public string AcquisitionType { get; }
        public double? UpperVoltageBound { get; }
        public double? LowerVoltageBound { get; }
        public double? UpperCurrentBound { get; }
        public double? LowerCurrentBound { get; }
        public double? VoltageMeasuringRange { get; }
        public double? CurrentMeasuringRange { get; }
        public long MeasureDelayMs { get; }
        public string? Filter50 { get; }
        public string? Scanner { get; }
        public string? Wires { get; }
        public string? Guard1 { get; }
        public List<string> NetStimOut { get; }
        public List<string> NetStimSense { get; }
        public List<string> NetGndOut { get; }
        public List<string> NetGndSense { get; }
        public Dictionary<string, string> Parameters { get; }

        public static Am2aDefinition FromParameters(Dictionary<string, string> parameters, string? typeId) =>
            new(parameters, typeId);

        private string? GetParameter(string key)
        {
            return Parameters.TryGetValue(key, out var value) ? value : null;
        }

        private static List<string> SplitNets(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        private static double? ParseMeasurement(string? text, string defaultUnit)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!TryParseMeasurement(text, out var value, out var unit))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(unit))
            {
                return value;
            }

            return defaultUnit.Equals("V", StringComparison.OrdinalIgnoreCase) ||
                   defaultUnit.Equals("A", StringComparison.OrdinalIgnoreCase) ||
                   defaultUnit.Equals("Ohm", StringComparison.OrdinalIgnoreCase)
                ? value
                : value;
        }
    }

    private static Encoding GetAm2aEncoding()
    {
        try
        {
            return Encoding.GetEncoding(1252);
        }
        catch (NotSupportedException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }
}
