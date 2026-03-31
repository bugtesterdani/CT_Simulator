using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx ICT component tests against the external device simulator.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private static readonly Encoding IctEncoding;

    static Ct3xxProgramSimulator()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        IctEncoding = Encoding.GetEncoding(1252);
    }

    /// <summary>
    /// Executes RunIctTest.
    /// </summary>
    private TestOutcome RunIctTest(Test test)
    {
        if (_fileSet == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ICT ohne geladenes Programm.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ICT ohne aktive Geraetesimulation.");
            return TestOutcome.Error;
        }

        if (!TryLoadIctDocument(test, out var ictDocument, out var error))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: error ?? "ICT-Datei konnte nicht geladen werden.");
            return TestOutcome.Error;
        }

        var (defaults, components) = ParseIctDocument(ictDocument);
        if (components.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ICT enthaelt keine Komponenten.");
            return TestOutcome.Error;
        }

        var overall = TestOutcome.Pass;
        foreach (var component in components)
        {
            if (!component.IsEnabled)
            {
                continue;
            }

            var metrics = BuildIctMetrics(component, defaults);
            if (metrics.Count == 0)
            {
                PublishStepEvaluation(
                    test,
                    TestOutcome.Error,
                    details: $"ICT {component.DisplayName}: keine Grenzwerte vorhanden.",
                    stepNameOverride: component.DisplayName);
                overall = CombineOutcomes(overall, TestOutcome.Error);
                continue;
            }

            foreach (var metric in metrics)
            {
                var outcome = EvaluateIctMetric(component, metric, out var measuredValue, out var details, out var traces, out var unit);
                PublishStepEvaluation(
                    test,
                    outcome,
                    measured: measuredValue,
                    lower: metric.LowerLimit,
                    upper: metric.UpperLimit,
                    unit: unit,
                    details: details,
                    traces: traces,
                    curvePoints: CaptureCurvePoints(),
                    stepNameOverride: metric.Label);
                overall = CombineOutcomes(overall, outcome);
            }
        }

        return overall;
    }

    /// <summary>
    /// Executes TryLoadIctDocument.
    /// </summary>
    private bool TryLoadIctDocument(Test test, out IctDocument document, out string? error)
    {
        var fileName = test.File?.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(fileName))
        {
            document = null!;
            error = "ICT ohne referenzierte .ctict-Datei.";
            return false;
        }

        if (_fileSet != null)
        {
            var match = _fileSet.GetDocuments<IctDocument>()
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
            error = $"ICT-Datei nicht gefunden: {resolved}";
            return false;
        }

        var lines = File.ReadAllLines(resolved, IctEncoding);
        document = new IctDocument(resolved, null, lines);
        error = null;
        return true;
    }

    /// <summary>
    /// Initializes a new instance of static.
    /// </summary>
    private static (IctDefaults Defaults, List<IctComponent> Components) ParseIctDocument(IctDocument document)
    {
        var defaults = new IctDefaults();
        var components = new List<IctComponent>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentType = null;
        var insideTest = false;
        var insideParameters = false;

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

            if (!insideTest && TryReadGlobalSetting(trimmed, defaults))
            {
                continue;
            }

            if (insideTest && trimmed.StartsWith("typeid", StringComparison.OrdinalIgnoreCase))
            {
                var (_, value) = ParseKeyValue(trimmed);
                currentType = value;
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
                    components.Add(IctComponent.FromParameters(current, currentType));
                }

                insideParameters = false;
                continue;
            }

            if (trimmed == "}" && insideTest)
            {
                insideTest = false;
                currentType = null;
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

        return (defaults, components);
    }

    private static bool TryReadGlobalSetting(string trimmed, IctDefaults defaults)
    {
        var (key, value) = ParseKeyValue(trimmed);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        switch (key.Trim())
        {
            case "DefaultToleranceR":
                defaults.DefaultToleranceR = value;
                return true;
            case "DefaultToleranceL":
                defaults.DefaultToleranceL = value;
                return true;
            case "DefaultToleranceC":
                defaults.DefaultToleranceC = value;
                return true;
        }

        return false;
    }

    private List<IctMetric> BuildIctMetrics(IctComponent component, IctDefaults defaults)
    {
        var metrics = new List<IctMetric>();
        if (TryParseMeasurement(component.Value, out var nominal, out var unit))
        {
            var tolerance = component.Tolerance;
            if (string.IsNullOrWhiteSpace(tolerance))
            {
                tolerance = defaults.ResolveDefaultTolerance(unit);
            }

            var (lower, upper) = BuildToleranceLimits(nominal, tolerance, component.ToleranceAbs);
            metrics.Add(new IctMetric(component, "value", component.DisplayName, nominal, lower, upper, unit));
        }

        foreach (var bound in component.Bounds)
        {
            if (!TryParseMeasurement(bound.Value, out var numeric, out var boundUnit))
            {
                continue;
            }

            var existing = metrics.FirstOrDefault(item => item.Key == bound.Key);
            if (existing == null)
            {
                metrics.Add(new IctMetric(component, bound.Key, $"{component.DisplayName} ({bound.Label})", null, null, null, boundUnit)
                {
                    LowerLimit = bound.IsLower ? numeric : null,
                    UpperLimit = bound.IsLower ? null : numeric
                });
            }
            else
            {
                if (bound.IsLower)
                {
                    existing.LowerLimit = numeric;
                }
                else
                {
                    existing.UpperLimit = numeric;
                }
            }
        }

        return metrics;
    }

    private TestOutcome EvaluateIctMetric(IctComponent component, IctMetric metric, out double? measuredValue, out string details, out IReadOnlyList<StepConnectionTrace> traces, out string unit)
    {
        measuredValue = null;
        unit = metric.Unit ?? string.Empty;
        traces = BuildIctTraces(component);

        var payload = BuildIctPayload(component, metric);
        if (!_externalDeviceSession!.TrySendInterface("ICT", payload, _cancellationToken, out var responsePayload, out var error, _simulatedTimeMs))
        {
            details = $"ICT {component.DisplayName}: Messung nicht moeglich ({error ?? "keine Antwort"}).";
            return TestOutcome.Error;
        }

        if (!TryExtractIctMeasurement(responsePayload, metric.Key, out var value, out var responseDetails, out var responseError))
        {
            details = $"ICT {component.DisplayName}: Messung nicht moeglich ({responseError ?? "kein Messwert"}).";
            return TestOutcome.Error;
        }

        measuredValue = value;
        var within = IsWithinLimits(value, metric.LowerLimit, metric.UpperLimit);
        var limitText = FormatLimitText(metric.LowerLimit, metric.UpperLimit, metric.Unit);
        details = $"ICT {component.DisplayName}: {metric.Label} = {FormatMeasurement(value, metric.Unit)}{limitText}{responseDetails}";
        return within ? TestOutcome.Pass : TestOutcome.Fail;
    }

    private IReadOnlyList<StepConnectionTrace> BuildIctTraces(IctComponent component)
    {
        if (_wireVizResolver == null)
        {
            return Array.Empty<StepConnectionTrace>();
        }

        var traces = new List<StepConnectionTrace>();
        foreach (var net in component.GetAllNets())
        {
            traces.AddRange(CollectSignalTraces(net, "ICT"));
        }

        return traces
            .DistinctBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static JsonObject BuildIctPayload(IctComponent component, IctMetric metric)
    {
        return new JsonObject
        {
            ["name"] = component.Name,
            ["reference"] = component.DrawingReference,
            ["type_id"] = component.TypeId,
            ["metric"] = metric.Key,
            ["label"] = metric.Label,
            ["unit"] = metric.Unit ?? string.Empty,
            ["nominal"] = metric.Nominal.HasValue ? metric.Nominal.Value : null,
            ["lower"] = metric.LowerLimit.HasValue ? metric.LowerLimit.Value : null,
            ["upper"] = metric.UpperLimit.HasValue ? metric.UpperLimit.Value : null,
            ["parameters"] = new JsonObject(component.Parameters.ToDictionary(kvp => kvp.Key, kvp => (JsonNode?)kvp.Value)),
            ["nets"] = component.BuildNetPayload()
        };
    }

    private static bool TryExtractIctMeasurement(object? payload, string metricKey, out double value, out string details, out string? error)
    {
        details = string.Empty;
        error = null;
        value = 0d;

        if (payload is JsonObject objectPayload)
        {
            if (objectPayload["error"] != null)
            {
                error = objectPayload["error"]?.ToString();
                return false;
            }

            if (objectPayload["measurements"] is JsonObject measurements &&
                measurements.TryGetPropertyValue(metricKey, out var metricNode) &&
                TryReadNumeric(metricNode, out value))
            {
                details = BuildResponseDetails(objectPayload);
                return true;
            }

            if (objectPayload.TryGetPropertyValue("value", out var valueNode) && TryReadNumeric(valueNode, out value))
            {
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

        error = "kein Messwert im ICT-Response";
        return false;
    }

    private static bool TryReadNumeric(JsonNode? node, out double value)
    {
        value = 0d;
        if (node == null)
        {
            return false;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out var numeric))
            {
                value = numeric;
                return true;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                value = intValue;
                return true;
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                value = longValue;
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static string BuildResponseDetails(JsonObject payload)
    {
        if (payload["details"] == null)
        {
            return string.Empty;
        }

        var text = payload["details"]?.ToString();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $" ({text})";
    }

    private static bool IsWithinLimits(double value, double? lower, double? upper)
    {
        if (lower.HasValue && upper.HasValue && lower.Value > upper.Value)
        {
            (lower, upper) = (upper, lower);
        }

        if (lower.HasValue && value < lower.Value)
        {
            return false;
        }

        if (upper.HasValue && value > upper.Value)
        {
            return false;
        }

        return true;
    }

    private static (double? Lower, double? Upper) BuildToleranceLimits(double nominal, string? toleranceText, string? absoluteText)
    {
        double? lower = null;
        double? upper = null;

        if (!string.IsNullOrWhiteSpace(toleranceText))
        {
            var (lowFactor, highFactor) = ParseTolerancePercent(toleranceText);
            if (lowFactor.HasValue)
            {
                lower = nominal * (1 + lowFactor.Value);
            }

            if (highFactor.HasValue)
            {
                upper = nominal * (1 + highFactor.Value);
            }
        }

        if (string.IsNullOrWhiteSpace(absoluteText))
        {
            return (lower, upper);
        }

        if (!TryParseMeasurement(absoluteText, out var absValue, out _))
        {
            return (lower, upper);
        }

        lower = nominal - absValue;
        upper = nominal + absValue;
        return (lower, upper);
    }

    private static (double? LowFactor, double? HighFactor) ParseTolerancePercent(string text)
    {
        var cleaned = text.Replace("%", string.Empty).Replace("±", string.Empty).Trim();
        cleaned = cleaned.Replace(",", ".");
        var parts = cleaned.Split(new[] { '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var symmetric))
        {
            var factor = symmetric / 100d;
            return (-factor, factor);
        }

        if (parts.Length >= 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var low) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var high))
        {
            return (low / 100d, high / 100d);
        }

        return (null, null);
    }

    private static string StripComments(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    private static (string? Key, string Value) ParseKeyValue(string line)
    {
        var brace = line.IndexOf('{');
        if (brace <= 0)
        {
            return (null, string.Empty);
        }

        var key = line[..brace].Trim();
        var firstQuote = line.IndexOf('"');
        var lastQuote = line.LastIndexOf('"');
        if (firstQuote < 0 || lastQuote <= firstQuote)
        {
            return (key, string.Empty);
        }

        var value = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        return (key, value);
    }

    /// <summary>
    /// Executes TryParseMeasurement.
    /// </summary>
    private static bool TryParseMeasurement(string? text, out double value, out string unit)
    {
        value = 0;
        unit = string.Empty;
        var cleaned = CleanMeasurementText(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        var parts = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var magnitude))
        {
            return false;
        }

        var unitPart = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
        var multiplier = ExtractPrefix(unitPart, out var normalizedUnit);
        value = magnitude * multiplier;
        unit = normalizedUnit;
        return true;
    }

    /// <summary>
    /// Executes CleanMeasurementText.
    /// </summary>
    private static string CleanMeasurementText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace("Ã¦", "µ").Replace("µ", "u");
        cleaned = cleaned.Replace("Ohm", "Ohm");
        return cleaned.Trim();
    }

    /// <summary>
    /// Executes ExtractPrefix.
    /// </summary>
    private static double ExtractPrefix(string unit, out string normalizedUnit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            normalizedUnit = unit;
            return 1d;
        }

        var trimmed = unit.Trim();
        var prefix = trimmed.Length > 1 ? trimmed[..1] : string.Empty;
        var tail = trimmed.Length > 1 ? trimmed[1..] : trimmed;

        normalizedUnit = trimmed;
        return prefix switch
        {
            "p" => NormalizeUnit(tail, "F", 1e-12, ref normalizedUnit),
            "n" => NormalizeUnit(tail, "F", 1e-9, ref normalizedUnit),
            "u" => NormalizeUnit(tail, "F", 1e-6, ref normalizedUnit),
            "m" => NormalizeUnit(tail, string.Empty, 1e-3, ref normalizedUnit),
            "k" => NormalizeUnit(tail, "Ohm", 1e3, ref normalizedUnit),
            "M" => NormalizeUnit(tail, "Ohm", 1e6, ref normalizedUnit),
            _ => 1d
        };
    }

    /// <summary>
    /// Executes NormalizeUnit.
    /// </summary>
    private static double NormalizeUnit(string tail, string defaultUnit, double factor, ref string normalizedUnit)
    {
        normalizedUnit = string.IsNullOrWhiteSpace(tail) ? defaultUnit : tail.Trim();
        return factor;
    }

    /// <summary>
    /// Executes FormatMeasurement.
    /// </summary>
    private static string FormatMeasurement(double? value, string? unit)
    {
        if (!value.HasValue)
        {
            return "n/a";
        }

        var unitText = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        return $"{value.Value.ToString("0.###", CultureInfo.InvariantCulture)}{unitText}";
    }

    /// <summary>
    /// Executes FormatLimitText.
    /// </summary>
    private static string FormatLimitText(double? lower, double? upper, string? unit)
    {
        if (!lower.HasValue && !upper.HasValue)
        {
            return string.Empty;
        }

        var unitText = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        var lowerText = lower.HasValue ? lower.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
        var upperText = upper.HasValue ? upper.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
        return $" (Limits {lowerText} .. {upperText}{unitText})";
    }

    private sealed class IctDefaults
    {
        /// <summary>
        /// Gets or sets DefaultToleranceR.
        /// </summary>
        public string? DefaultToleranceR { get; set; }
        /// <summary>
        /// Gets or sets DefaultToleranceL.
        /// </summary>
        public string? DefaultToleranceL { get; set; }
        /// <summary>
        /// Gets or sets DefaultToleranceC.
        /// </summary>
        public string? DefaultToleranceC { get; set; }

        /// <summary>
        /// Executes ResolveDefaultTolerance.
        /// </summary>
        public string? ResolveDefaultTolerance(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return null;
            }

            if (unit.Contains("Ohm", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultToleranceR;
            }

            if (unit.Contains("H", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultToleranceL;
            }

            if (unit.Contains("F", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultToleranceC;
            }

            return null;
        }
    }

    private sealed class IctComponent
    {
        /// <summary>
        /// Initializes a new instance of IctComponent.
        /// </summary>
        private IctComponent(Dictionary<string, string> parameters, string? typeId)
        {
            Parameters = parameters;
            TypeId = typeId ?? string.Empty;
            Name = GetParameter("Name") ?? GetParameter("DrawingReference") ?? "ICT";
            DrawingReference = GetParameter("DrawingReference") ?? Name;
            Value = GetParameter("Value");
            Tolerance = GetParameter("Tolerance");
            ToleranceAbs = GetParameter("ToleranceAbs") ?? GetParameter("ToleranceAbsR") ?? GetParameter("ToleranceAbsL") ?? GetParameter("ToleranceAbsC");
            IsEnabled = !string.Equals(GetParameter("State"), "disabled", StringComparison.OrdinalIgnoreCase);
            Bounds = BuildBounds();
        }

        /// <summary>
        /// Gets or sets TypeId.
        /// </summary>
        public string TypeId { get; }
        /// <summary>
        /// Gets or sets Name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Gets or sets DrawingReference.
        /// </summary>
        public string DrawingReference { get; }
        /// <summary>
        /// Gets or sets Value.
        /// </summary>
        public string? Value { get; }
        /// <summary>
        /// Gets or sets Tolerance.
        /// </summary>
        public string? Tolerance { get; }
        /// <summary>
        /// Gets or sets ToleranceAbs.
        /// </summary>
        public string? ToleranceAbs { get; }
        /// <summary>
        /// Gets or sets IsEnabled.
        /// </summary>
        public bool IsEnabled { get; }
        /// <summary>
        /// Gets or sets Bounds.
        /// </summary>
        public IReadOnlyList<IctBound> Bounds { get; }
        /// <summary>
        /// Gets or sets Parameters.
        /// </summary>
        public Dictionary<string, string> Parameters { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DrawingReference : Name;

        /// <summary>
        /// Executes FromParameters.
        /// </summary>
        public static IctComponent FromParameters(Dictionary<string, string> parameters, string? typeId) =>
            new(parameters, typeId);

        /// <summary>
        /// Executes GetAllNets.
        /// </summary>
        public IEnumerable<string> GetAllNets()
        {
            foreach (var key in new[] { "Net.Stim.Out", "Net.Stim.Sense", "Net.Gnd.Out", "Net.Gnd.Sense" })
            {
                if (Parameters.TryGetValue(key, out var value))
                {
                    foreach (var net in SplitNets(value))
                    {
                        yield return net;
                    }
                }
            }
        }

        /// <summary>
        /// Executes BuildNetPayload.
        /// </summary>
        public JsonObject BuildNetPayload()
        {
            var payload = new JsonObject();
            foreach (var key in new[] { "Net.Stim.Out", "Net.Stim.Sense", "Net.Gnd.Out", "Net.Gnd.Sense" })
            {
                if (Parameters.TryGetValue(key, out var value))
                {
                    payload[key] = new JsonArray(SplitNets(value).Select(net => (JsonNode?)net).ToArray());
                }
            }

            return payload;
        }

        /// <summary>
        /// Executes BuildBounds.
        /// </summary>
        private IReadOnlyList<IctBound> BuildBounds()
        {
            var bounds = new List<IctBound>();
            foreach (var (key, value) in Parameters)
            {
                if (key.StartsWith("Upper", StringComparison.OrdinalIgnoreCase) && key.EndsWith("Bound", StringComparison.OrdinalIgnoreCase))
                {
                    bounds.Add(new IctBound(key, value, false));
                }
                else if (key.StartsWith("Lower", StringComparison.OrdinalIgnoreCase) && key.EndsWith("Bound", StringComparison.OrdinalIgnoreCase))
                {
                    bounds.Add(new IctBound(key, value, true));
                }
            }

            return bounds;
        }

        /// <summary>
        /// Executes GetParameter.
        /// </summary>
        private string? GetParameter(string key)
        {
            return Parameters.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Executes SplitNets.
        /// </summary>
        private static IEnumerable<string> SplitNets(string raw)
        {
            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }
    }

    private sealed class IctBound
    {
        /// <summary>
        /// Initializes a new instance of IctBound.
        /// </summary>
        public IctBound(string rawKey, string value, bool isLower)
        {
            RawKey = rawKey;
            Value = value;
            IsLower = isLower;
            Key = NormalizeKey(rawKey);
            Label = Key switch
            {
                "voltage" => "Voltage",
                "current" => "Current",
                "resistance" => "Resistance",
                _ => rawKey.Replace("Upper", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("Lower", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("Bound", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim()
            };
        }

        /// <summary>
        /// Gets or sets RawKey.
        /// </summary>
        public string RawKey { get; }
        /// <summary>
        /// Gets or sets Value.
        /// </summary>
        public string Value { get; }
        /// <summary>
        /// Gets or sets IsLower.
        /// </summary>
        public bool IsLower { get; }
        /// <summary>
        /// Gets or sets Key.
        /// </summary>
        public string Key { get; }
        /// <summary>
        /// Gets or sets Label.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Executes NormalizeKey.
        /// </summary>
        private static string NormalizeKey(string rawKey)
        {
            var stripped = rawKey.Replace("Upper", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Lower", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Bound", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (stripped.Equals("Voltage", StringComparison.OrdinalIgnoreCase))
            {
                return "voltage";
            }

            if (stripped.Equals("Current", StringComparison.OrdinalIgnoreCase))
            {
                return "current";
            }

            if (stripped.Equals("Resistance", StringComparison.OrdinalIgnoreCase))
            {
                return "resistance";
            }

            return stripped.ToLowerInvariant();
        }
    }

    private sealed class IctMetric
    {
        /// <summary>
        /// Initializes a new instance of IctMetric.
        /// </summary>
        public IctMetric(IctComponent component, string key, string label, double? nominal, double? lower, double? upper, string? unit)
        {
            Component = component;
            Key = key;
            Label = label;
            Nominal = nominal;
            LowerLimit = lower;
            UpperLimit = upper;
            Unit = unit;
        }

        /// <summary>
        /// Gets or sets Component.
        /// </summary>
        public IctComponent Component { get; }
        /// <summary>
        /// Gets or sets Key.
        /// </summary>
        public string Key { get; }
        /// <summary>
        /// Gets or sets Label.
        /// </summary>
        public string Label { get; }
        /// <summary>
        /// Gets or sets Nominal.
        /// </summary>
        public double? Nominal { get; }
        /// <summary>
        /// Gets or sets LowerLimit.
        /// </summary>
        public double? LowerLimit { get; set; }
        /// <summary>
        /// Gets or sets UpperLimit.
        /// </summary>
        public double? UpperLimit { get; set; }
        /// <summary>
        /// Gets or sets Unit.
        /// </summary>
        public string? Unit { get; }
    }
}
