using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx SHRT shortcut tests against the active wiring and simulation state.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private TestOutcome RunShortcutTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne Parameter.");
            return TestOutcome.Error;
        }

        if (_wireVizResolver == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne geladene Verdrahtung.");
            return TestOutcome.Error;
        }

        var testpoints = LoadShortcutTestpoints(parameters);
        if (testpoints.Count < 2)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne ausreichende Testpunkte.");
            return TestOutcome.Error;
        }

        var resolver = ResolveShortcutWireViz();
        if (resolver == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne geladene Verdrahtung.");
            return TestOutcome.Error;
        }

        var threshold = ParseEngineeringValue(GetParameterAttribute(parameters, "ResThreshold"))
            ?? ParseEngineeringValue(GetParameterAttribute(parameters, "DefThreshold"));
        if (!threshold.HasValue)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne ResThreshold/DefThreshold.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT ohne aktive Geraetesimulation.");
            return TestOutcome.Error;
        }

        var errors = new List<string>();
        var pairTraces = new Dictionary<string, WireVizResistanceMeasurement>(StringComparer.OrdinalIgnoreCase);
        var pairList = BuildShortcutPairs(resolver, testpoints, pairTraces, errors);

        if (errors.Count > 0)
        {
            var preview = string.Join("; ", errors.Take(4));
            var suffix = errors.Count > 4 ? $" (+{errors.Count - 4} weitere)" : string.Empty;
            PublishStepEvaluation(test, TestOutcome.Error, details: $"SHRT: Verdrahtung unvollstaendig: {preview}{suffix}");
            return TestOutcome.Error;
        }

        if (pairList.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SHRT: keine messbaren Testpunkt-Paare.");
            return TestOutcome.Error;
        }

        if (!TryQueryShortcutMeasurements(pairList, threshold.Value, out var shortMeasurements, out var errorDetails))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: errorDetails ?? "SHRT: keine Messdaten vom DUT.");
            return TestOutcome.Error;
        }

        var knownPairs = BuildKnownShortPairs(parameters);
        var unexpected = new List<ShortcutFinding>();
        var measurementMap = shortMeasurements
            .Where(item => !string.IsNullOrWhiteSpace(item.Source) && !string.IsNullOrWhiteSpace(item.Target))
            .GroupBy(item => NormalizePair(item.Source, item.Target), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var pair in pairList)
        {
            var key = NormalizePair(pair.Source, pair.Target);
            if (!measurementMap.TryGetValue(key, out var measurement) || !measurement.ResistanceOhms.HasValue)
            {
                errors.Add($"{pair.Source} <-> {pair.Target}: kein Messwert.");
                continue;
            }

            if (!pairTraces.TryGetValue(key, out var traceMeasurement))
            {
                continue;
            }

            if (measurement.ResistanceOhms.Value <= threshold.Value &&
                !knownPairs.Contains(key))
            {
                unexpected.Add(new ShortcutFinding(pair.Source, pair.Target, traceMeasurement)
                {
                    ReportedResistanceOhms = measurement.ResistanceOhms
                });
            }
        }

        if (errors.Count > 0)
        {
            var preview = string.Join("; ", errors.Take(4));
            var suffix = errors.Count > 4 ? $" (+{errors.Count - 4} weitere)" : string.Empty;
            PublishStepEvaluation(test, TestOutcome.Error, details: $"SHRT: Messdaten unvollstaendig: {preview}{suffix}");
            return TestOutcome.Error;
        }

        if (unexpected.Count == 0)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Pass,
                details: $"SHRT: keine unerwarteten Kurzschluesse (Testpunkte={testpoints.Count}).");
            return TestOutcome.Pass;
        }

        foreach (var finding in unexpected)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Fail,
                measured: finding.ReportedResistanceOhms ?? finding.Measurement.ResistanceOhms,
                upper: threshold,
                unit: "Ohm",
                details: BuildShortcutDetailText(finding, threshold.Value),
                traces: BuildShortcutTrace(finding.Measurement),
                stepNameOverride: $"{finding.Source} -> {finding.Target}");
        }

        return TestOutcome.Fail;
    }

    private WireVizHarnessResolver? ResolveShortcutWireViz()
    {
        if (_fileSet == null)
        {
            return _wireVizResolver;
        }

        if (_deviceCtctResistors.Count == 0)
        {
            return _wireVizResolver;
        }

        return WireVizHarnessResolver.Create(_fileSet);
    }

    private List<ShortcutMeasurement> BuildShortcutPairs(
        WireVizHarnessResolver resolver,
        IReadOnlyList<string> testpoints,
        Dictionary<string, WireVizResistanceMeasurement> pairTraces,
        List<string> errors)
    {
        var results = new List<ShortcutMeasurement>();

        foreach (var testpoint in testpoints)
        {
            if (!resolver.TryResolve(testpoint, out _))
            {
                errors.Add($"{testpoint}: Testpunkt konnte nicht aufgeloest werden.");
            }
        }

        if (errors.Count > 0)
        {
            return results;
        }

        for (var i = 0; i < testpoints.Count; i++)
        {
            var source = testpoints[i];
            for (var j = i + 1; j < testpoints.Count; j++)
            {
                var target = testpoints[j];
                var traceMeasurement = resolver.MeasureResistance(
                    source,
                    target,
                    _signalState,
                    _signalChangedAtMs,
                    _simulatedTimeMs,
                    _faults);

                var key = NormalizePair(source, target);
                pairTraces[key] = traceMeasurement;
                results.Add(new ShortcutMeasurement(source, target, null));
            }
        }

        return results;
    }

    private static List<string> LoadShortcutTestpoints(TestParameters parameters)
    {
        var testpoints = new List<string>();
        foreach (var table in parameters.Tables.Where(table =>
                     table.Id != null && table.Id.StartsWith("STP", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var record in table.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = GetRecordAttribute(record, "Name") ?? record.TestPoint ?? record.Text;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foreach (var item in SplitTestpointList(name))
                {
                    testpoints.Add(item);
                }
            }
        }

        return testpoints
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> BuildKnownShortPairs(TestParameters parameters)
    {
        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in parameters.Tables.Where(table =>
                     table.Id != null && table.Id.StartsWith("SSH", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var record in table.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var group1 = SplitTestpointList(GetRecordAttribute(record, "Testpoint1"));
                var group2 = SplitTestpointList(GetRecordAttribute(record, "Testpoint2"));
                if (group1.Count == 0 && group2.Count == 0)
                {
                    continue;
                }

                if (group2.Count == 0)
                {
                    group2 = group1;
                }

                foreach (var first in group1)
                {
                    foreach (var second in group2)
                    {
                        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                        {
                            continue;
                        }

                        if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        pairs.Add(NormalizePair(first, second));
                    }
                }
            }
        }

        return pairs;
    }

    private static IReadOnlyList<string> SplitTestpointList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static string NormalizePair(string first, string second)
    {
        var order = string.Compare(first, second, StringComparison.OrdinalIgnoreCase);
        return order <= 0 ? $"{first}|{second}" : $"{second}|{first}";
    }

    private static string BuildShortcutDetailText(ShortcutFinding finding, double thresholdOhms)
    {
        var elementText = finding.Measurement.EdgeDescriptions.Count == 0
            ? string.Empty
            : $" via {string.Join(", ", finding.Measurement.EdgeDescriptions)}";
        var measured = FormatEngineeringValue(finding.ReportedResistanceOhms ?? finding.Measurement.ResistanceOhms);
        var threshold = FormatEngineeringValue(thresholdOhms);
        return $"SHRT: unerwarteter Kurzschluss {finding.Source}->{finding.Target} = {measured} (Limit <= {threshold}){elementText}";
    }

    private static IReadOnlyList<StepConnectionTrace> BuildShortcutTrace(WireVizResistanceMeasurement measurement)
    {
        if (!measurement.PathFound || measurement.Nodes.Count == 0)
        {
            return Array.Empty<StepConnectionTrace>();
        }

        var title = $"SHRT: {measurement.SourceSignalName} -> {measurement.TargetSignalName} ({FormatEngineeringValue(measurement.ResistanceOhms)})";
        return new[] { new StepConnectionTrace(title, measurement.Nodes) };
    }

    private sealed record ShortcutFinding(string Source, string Target, WireVizResistanceMeasurement Measurement)
    {
        public double? ReportedResistanceOhms { get; init; }
    }

    private bool TryQueryShortcutMeasurements(
        IReadOnlyList<ShortcutMeasurement> pairs,
        double thresholdOhms,
        out List<ShortcutMeasurement> measurements,
        out string? errorDetails)
    {
        measurements = new List<ShortcutMeasurement>();
        errorDetails = null;

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["threshold"] = thresholdOhms,
            ["pairs"] = new System.Text.Json.Nodes.JsonArray(pairs.Select(pair =>
                (System.Text.Json.Nodes.JsonNode?)new System.Text.Json.Nodes.JsonObject
                {
                    ["a"] = pair.Source,
                    ["b"] = pair.Target
                }).ToArray())
        };

        if (!_externalDeviceSession!.TrySendInterface("SHRT", payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            errorDetails = $"SHRT: Messung nicht moeglich ({error ?? "keine Antwort"}).";
            return false;
        }

        if (!TryExtractShortcutMeasurements(responsePayload, measurements, out var parseError))
        {
            errorDetails = parseError ?? "SHRT: keine Messdaten im DUT-Response.";
            return false;
        }

        return measurements.Count > 0;
    }

    private static bool TryExtractShortcutMeasurements(object? payload, List<ShortcutMeasurement> measurements, out string? error)
    {
        error = null;
        if (payload is System.Text.Json.Nodes.JsonObject objectPayload)
        {
            if (objectPayload["error"] != null)
            {
                error = objectPayload["error"]?.ToString();
                return false;
            }

            if (objectPayload["errors"] is System.Text.Json.Nodes.JsonArray errorArray)
            {
                var entries = errorArray.Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
                if (entries.Count > 0)
                {
                    error = string.Join("; ", entries);
                    return false;
                }
            }

            if (objectPayload["shorts"] is System.Text.Json.Nodes.JsonArray shortsArray)
            {
                foreach (var entry in shortsArray.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var source = entry["a"]?.ToString() ?? entry["source"]?.ToString();
                    var target = entry["b"]?.ToString() ?? entry["target"]?.ToString();
                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }

                    var ohms = TryReadNumeric(entry["ohms"]);
                    measurements.Add(new ShortcutMeasurement(source.Trim(), target.Trim(), ohms));
                }
            }

            if (objectPayload["measurements"] is System.Text.Json.Nodes.JsonObject measurementMap)
            {
                foreach (var pair in measurementMap)
                {
                    if (!TrySplitPair(pair.Key, out var source, out var target))
                    {
                        continue;
                    }

                    var ohms = TryReadNumeric(pair.Value);
                    measurements.Add(new ShortcutMeasurement(source, target, ohms));
                }
            }

            return measurements.Count > 0;
        }

        error = "SHRT: Response ist kein Objekt.";
        return false;
    }

    private static bool TrySplitPair(string key, out string source, out string target)
    {
        source = string.Empty;
        target = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var separators = new[] { '|', ',', ';' };
        var parts = key.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        source = parts[0].Trim();
        target = parts[1].Trim();
        return source.Length > 0 && target.Length > 0;
    }

    private static double? TryReadNumeric(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is System.Text.Json.Nodes.JsonValue value)
        {
            if (value.TryGetValue<double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (value.TryGetValue<string>(out var text) &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private sealed record ShortcutMeasurement(string Source, string Target, double? ResistanceOhms);
}
