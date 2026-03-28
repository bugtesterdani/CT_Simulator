using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes DM30 digital pattern tests.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private TestOutcome RunDm30DigitalPatternTest(Test test, string patternPath)
    {
        var stepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "DM30";
        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-Geraetesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }
        if (!File.Exists(patternPath))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Patterndatei fehlt: {Path.GetFileName(patternPath)}");
            return TestOutcome.Error;
        }

        if (!Dm30PatternParser.TryParse(patternPath, out var document, out var parseError))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Pattern konnte nicht gelesen werden: {parseError}");
            return TestOutcome.Error;
        }

        if (document.TestSteps <= 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DM30 Pattern enthaelt keine Testschritte.");
            return TestOutcome.Error;
        }

        var startStep = Math.Max(1, document.TestStart > 0 ? document.TestStart : 1);
        var endStep = Math.Min(document.TestSteps, document.TestEnd > 0 ? document.TestEnd : document.TestSteps);
        if (endStep < startStep)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Patternbereich ungueltig: {startStep}..{endStep}");
            return TestOutcome.Error;
        }

        var acquisitionSignals = document.AcquisitionGroups
            .SelectMany(group => group.Signals)
            .Where(signal => signal.IsUsed)
            .ToList();

        if (acquisitionSignals.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DM30 Pattern enthaelt keine Acquisition-Signale.");
            return TestOutcome.Error;
        }

        var evaluationResults = new List<Dm30SignalEvaluation>();
        var overallMismatch = 0;
        var traces = new List<StepConnectionTrace>();
        var curvePoints = new List<MeasurementCurvePoint>();
        var stepDurationMs = Math.Max(1L, (long)Math.Round(1000.0d / Math.Max(1.0d, document.TestFrequencyHz), MidpointRounding.AwayFromZero));

        var stimuliPayload = new List<Dictionary<string, object?>>();
        foreach (var group in document.StimuliGroups)
        {
            foreach (var signal in group.Signals.Where(signal => signal.IsUsed))
            {
                var patternBits = Dm30PatternParser.ParseHexToBits(signal.StimuliPattern, document.TestSteps);
                if (patternBits == null)
                {
                    PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Stimuli-Pattern ungueltig ({signal.Name}).");
                    return TestOutcome.Error;
                }

                var lastValue = patternBits[Math.Clamp(endStep - 1, 0, patternBits.Count - 1)];
                RememberSignal(signal.Name, lastValue);
                traces.AddRange(CollectSignalTraces(signal.Name, "DM30 Stimuli"));
                curvePoints.AddRange(BuildDm30CurvePoints(signal.Name, patternBits, startStep, endStep, stepDurationMs, _simulatedTimeMs, "DM30 Stimuli"));

                stimuliPayload.Add(new Dictionary<string, object?>
                {
                    ["name"] = signal.Name,
                    ["pattern_hex"] = signal.StimuliPattern ?? string.Empty,
                    ["high_level"] = group.HighLevel,
                    ["low_level"] = group.LowLevel
                });
            }
        }

        var acquisitionPayload = acquisitionSignals.Select(signal => new Dictionary<string, object?>
        {
            ["name"] = signal.Name,
            ["nominal_hex"] = signal.AcquisitionNominalPattern ?? string.Empty,
            ["mask_hex"] = signal.AcquisitionMaskPattern ?? string.Empty
        }).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["protocol"] = "dm30",
            ["operation"] = "pattern_evaluate",
            ["test_name"] = document.TestName,
            ["steps"] = document.TestSteps,
            ["start_step"] = startStep,
            ["end_step"] = endStep,
            ["frequency_hz"] = document.TestFrequencyHz,
            ["stimuli"] = stimuliPayload,
            ["acquisition"] = acquisitionPayload
        };

        if (!_externalDeviceSession.TrySendInterface("DM30", payload, _cancellationToken, out var responsePayload, out var interfaceError, _simulatedTimeMs))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Interface Fehler: {interfaceError}");
            return TestOutcome.Error;
        }

        RefreshExternalDeviceState();
        var response = responsePayload as System.Text.Json.Nodes.JsonObject;
        if (response == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DM30 Interface: Ungueltige Antwort.");
            return TestOutcome.Error;
        }

        if (response["status"]?.GetValue<string?>()?.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
        {
            var detail = response["details"]?.GetValue<string?>() ?? "DM30 Interface: Fehler.";
            PublishStepEvaluation(test, TestOutcome.Error, details: detail);
            return TestOutcome.Error;
        }

        var acquisitionResponse = ParseDm30AcquisitionResponse(response);
        if (acquisitionResponse == null || acquisitionResponse.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "DM30 Interface: Keine Acquisition-Daten geliefert.");
            return TestOutcome.Error;
        }

        foreach (var signal in acquisitionSignals)
        {
            var nominalBits = Dm30PatternParser.ParseHexToBits(signal.AcquisitionNominalPattern, document.TestSteps);
            if (nominalBits == null)
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Nominal-Pattern ungueltig ({signal.Name}).");
                return TestOutcome.Error;
            }

            var maskBits = Dm30PatternParser.ParseHexToBits(signal.AcquisitionMaskPattern, document.TestSteps) ??
                           Enumerable.Repeat(0, document.TestSteps).ToList();

            if (!acquisitionResponse.TryGetValue(signal.Name, out var actualHex))
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Interface: Acquisition fehlt fuer {signal.Name}.");
                return TestOutcome.Error;
            }

            var actualBits = Dm30PatternParser.ParseHexToBits(actualHex, document.TestSteps);
            if (actualBits == null)
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"DM30 Interface: Ungueltige Acquisition-Daten fuer {signal.Name}.");
                return TestOutcome.Error;
            }
            var mismatchCount = 0;
            var compared = 0;
            var firstMismatch = -1;
            for (var stepIndex = startStep - 1; stepIndex < endStep && stepIndex < nominalBits.Count; stepIndex++)
            {
                if (maskBits[stepIndex] != 0)
                {
                    continue;
                }

                compared++;
                if (actualBits[stepIndex] != nominalBits[stepIndex])
                {
                    mismatchCount++;
                    if (firstMismatch < 0)
                    {
                        firstMismatch = stepIndex + 1;
                    }
                }
            }

            overallMismatch += mismatchCount;
            evaluationResults.Add(new Dm30SignalEvaluation(signal.Name, mismatchCount, compared, firstMismatch, signal.AcquisitionNominalPattern, signal.AcquisitionMaskPattern));
            traces.AddRange(CollectSignalTraces(signal.Name, "DM30 Acquisition"));
            curvePoints.AddRange(BuildDm30CurvePoints(signal.Name, actualBits, startStep, endStep, stepDurationMs, _simulatedTimeMs, "DM30 Acquisition"));
        }

        var outcome = overallMismatch == 0 ? TestOutcome.Pass : TestOutcome.Fail;
        var details = BuildDm30DetailText(document, evaluationResults, overallMismatch);

        PublishStepEvaluation(
            test,
            outcome,
            measured: overallMismatch,
            lower: 0,
            upper: 0,
            unit: "bit",
            details: details,
            traces: traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            curvePoints: curvePoints);

        return outcome;
    }

    private static Dictionary<string, string>? ParseDm30AcquisitionResponse(System.Text.Json.Nodes.JsonObject response)
    {
        var acquisitionNode = response["acquisition"];
        if (acquisitionNode is System.Text.Json.Nodes.JsonObject acquisitionObject)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in acquisitionObject)
            {
                if (item.Value == null)
                {
                    continue;
                }

                var text = item.Value.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result[item.Key] = text;
                }
            }

            return result;
        }

        if (acquisitionNode is System.Text.Json.Nodes.JsonArray acquisitionArray)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in acquisitionArray.OfType<System.Text.Json.Nodes.JsonObject>())
            {
                var name = entry["name"]?.GetValue<string?>();
                var hex = entry["pattern_hex"]?.GetValue<string?>();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(hex))
                {
                    continue;
                }

                result[name] = hex;
            }

            return result;
        }

        return null;
    }

    private static IReadOnlyList<MeasurementCurvePoint> BuildDm30CurvePoints(string signalName, IReadOnlyList<int> bits, int startStep, int endStep, long stepDurationMs, long startTimeMs, string labelPrefix)
    {
        var points = new List<MeasurementCurvePoint>();
        var time = startTimeMs;
        for (var index = startStep - 1; index < endStep && index < bits.Count; index++)
        {
            points.Add(new MeasurementCurvePoint(time, $"{labelPrefix}: {signalName}", bits[index], "logic"));
            time += stepDurationMs;
        }

        return points;
    }

    private static string BuildDm30DetailText(Dm30PatternDocument document, IReadOnlyList<Dm30SignalEvaluation> evaluations, int overallMismatch)
    {
        var header = $"DM30 Schritte={document.TestSteps}, Bereich={document.TestStart}-{document.TestEnd}";
        var detailParts = new List<string>
        {
            $"DM30: MismatchBits={overallMismatch} bit [0..0], Signals={evaluations.Count}"
        };

        foreach (var evaluation in evaluations)
        {
            var mismatchText = evaluation.MismatchBits.ToString(CultureInfo.InvariantCulture);
            var comparedText = evaluation.ComparedBits.ToString(CultureInfo.InvariantCulture);
            var firstMismatch = evaluation.FirstMismatchStep > 0 ? evaluation.FirstMismatchStep.ToString(CultureInfo.InvariantCulture) : "-";
            detailParts.Add($"{evaluation.SignalName}: MismatchBits={mismatchText} bit [0..0], Compared={comparedText}, FirstMismatch={firstMismatch}");
        }

        return $"{header} | {string.Join("; ", detailParts)}";
    }

    private string? ResolveTestFilePath(Test test)
    {
        var fileName = NormalizeQuotedText(test.File);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var resolved = _fileSet?.ExternalFiles
            .Select(file => file.FilePath)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        var candidate = Path.Combine(_context.ProgramDirectory, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private sealed record Dm30SignalEvaluation(
        string SignalName,
        int MismatchBits,
        int ComparedBits,
        int FirstMismatchStep,
        string? NominalHex,
        string? MaskHex);
}
