using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Xml;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop.ViewModels;

public static class TestDetailsFactory
{
    private static readonly Encoding AnsiEncoding;
    private static readonly HashSet<string> IgnoredParameterAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name",
        "DrawingReference",
        "Message",
        "Image",
        "Mode",
        "Options",
        "OptionsVariable"
    };

    private static readonly HashSet<string> ArbMetadataIgnoredKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "TypeId",
        "Name",
        "DrawingReference"
    };

    private static readonly string[] ArbMetadataIgnoredPrefixes =
    {
        "ArbSample"
    };

    private static readonly HashSet<string> DisplayOptionAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShowOK",
        "ShowAbort",
        "ShowHotKeys"
    };

    static TestDetailsFactory()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AnsiEncoding = Encoding.GetEncoding(1252);
    }

    public static TestDetailsViewModel? Create(Test test, string? programDirectory)
    {
        var parameters = test.Parameters;
        var viewModel = new TestDetailsViewModel
        {
            Mode = Clean(parameters?.Mode),
            Message = Clean(parameters?.Message) ?? Clean(test.Name),
            Description = Clean(parameters?.DrawingReference) ?? Clean(test.Name),
            DisplayModeName = DetermineDisplayModeName(test, parameters),
            IsAm2Test = IsAm2Test(test),
            IsIctTest = IsIctTest(test),
            LibraryName = Clean(parameters?.Library),
            FunctionName = Clean(parameters?.Function),
            ExternalFile = Clean(test.File)
        };

        if (parameters != null)
        {
            foreach (var option in ParseOptions(parameters.Options))
            {
                viewModel.Options.Add(option);
            }

            foreach (var table in parameters.Tables)
            {
                foreach (var record in table.Records)
                {
                    var metric = CreateMetric(record);
                    viewModel.Records.Add(metric);
                }
            }

            AppendChannelDetails(parameters, viewModel);
            AppendAdditionalAttributes(parameters, viewModel);
            AppendDisplayOptions(parameters, viewModel);
        }

        AppendExternalDetails(test, viewModel, programDirectory);
        viewModel.FinalizeRecords();
        return viewModel;
    }

    private static TestRecordMetricViewModel CreateMetric(Record record)
    {
        var title = FirstValue(record.Text, record.DrawingReference, record.Destination, record.Variable, record.Id, "Eintrag");
        var destination = FirstValue(record.Destination, record.Variable, null);
        var expression = Clean(record.Expression);
        var unit = Clean(record.Unit);
        var recordType = Clean(record.Type);
        var lower = TryParseDouble(record.LowerLimit);
        var upper = TryParseDouble(record.UpperLimit);
        var additional = BuildAdditionalInfo(record);

        return new TestRecordMetricViewModel(
            Clean(title) ?? "Eintrag",
            Clean(destination),
            expression,
            unit,
            recordType,
            additional,
            lower,
            upper);
    }

    private static string? BuildAdditionalInfo(Record record)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.TestPoint))
        {
            parts.Add($"TP {record.TestPoint}");
        }

        if (!string.IsNullOrWhiteSpace(record.SwitchState))
        {
            parts.Add($"Schalter: {record.SwitchState}");
        }

        if (!string.IsNullOrWhiteSpace(record.Voltage))
        {
            parts.Add($"U: {record.Voltage}");
        }

        if (!string.IsNullOrWhiteSpace(record.Resistance))
        {
            parts.Add($"R: {record.Resistance}");
        }

        if (!string.IsNullOrWhiteSpace(record.Time))
        {
            parts.Add($"t: {record.Time}");
        }

        var reference = Clean(record.DrawingReference);
        if (!string.IsNullOrWhiteSpace(reference))
        {
            parts.Add(reference);
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static IEnumerable<string> ParseOptions(string? rawOptions)
    {
        if (string.IsNullOrWhiteSpace(rawOptions))
        {
            yield break;
        }

        var cleaned = rawOptions.Trim();
        if (cleaned.StartsWith("{", StringComparison.Ordinal) && cleaned.EndsWith("}", StringComparison.Ordinal))
        {
            cleaned = cleaned.Substring(1, cleaned.Length - 2);
        }

        foreach (var segment in cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var text = Clean(segment);
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }
        }
    }

internal static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if ((trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal)) ||
            (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal)))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        return WebUtility.HtmlDecode(trimmed).Trim();
    }

    private static string? FirstValue(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string? DetermineDisplayModeName(Test test, TestParameters? parameters)
    {
        if (!string.Equals(test.Id, "PRT^", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var mode = Clean(parameters?.Mode);
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "Display (unbekannter Modus)";
        }

        return mode switch
        {
            "Single selection" => "Display Query (Auswahl)",
            "Input values into variables" => "Display Input",
            "Query for PASS/FAIL" => "Display Query (PASS/FAIL)",
            "Display a message" => "Display Message",
            "Display results" => "Display Results",
            _ => $"Display ({mode})"
        };
    }

    private static double? TryParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static void AppendExternalDetails(Test test, TestDetailsViewModel viewModel, string? directory)
    {
        if (!TryResolveExternalFile(test.File, directory, out var path))
        {
            return;
        }

        viewModel.ExternalFile = path;

        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ctict":
                AppendIctDetails(path, viewModel);
                break;
            case ".ctarb":
                AppendArbDetails(path, viewModel);
                break;
        }
    }

    private static void AppendIctDetails(string filePath, TestDetailsViewModel viewModel)
    {
        var lines = File.ReadAllLines(filePath, AnsiEncoding);
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentType = null;
        var insideTest = false;
        var insideParameters = false;

        foreach (var rawLine in lines)
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
                    var metric = CreateIctMetric(current, currentType);
                    viewModel.Records.Add(metric);
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
    }

    private static TestRecordMetricViewModel CreateIctMetric(Dictionary<string, string> values, string? type)
    {
        values.TryGetValue("Name", out var title);
        values.TryGetValue("Value", out var measurement);
        values.TryGetValue("Tolerance", out var tolerance);
        values.TryGetValue("ToleranceAbsR", out var toleranceAbs);
        values.TryGetValue("DrawingReference", out var reference);

        double? measurementValue = null;
        string? measurementUnit = null;
        if (TryParseMeasurement(measurement, out var parsedValue, out var parsedUnit))
        {
            measurementValue = parsedValue;
            measurementUnit = parsedUnit;
        }

        double? lower = null;
        double? upper = null;
        if (measurementValue.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(tolerance))
            {
                var (lowFactor, highFactor) = ParseTolerancePercent(tolerance);
                if (lowFactor.HasValue)
                {
                    lower = measurementValue.Value * (1 + lowFactor.Value);
                }

                if (highFactor.HasValue)
                {
                    upper = measurementValue.Value * (1 + highFactor.Value);
                }
            }
            else if (!string.IsNullOrWhiteSpace(toleranceAbs) &&
                     TryParseMeasurement(toleranceAbs, out var abs, out _))
            {
                lower = measurementValue.Value - abs;
                upper = measurementValue.Value + abs;
            }
        }

        var infoParts = new List<string>();
        void AppendInfo(string key, string label)
        {
            if (values.TryGetValue(key, out var info) && !string.IsNullOrWhiteSpace(info))
            {
                infoParts.Add($"{label}: {info}");
            }
        }

        AppendInfo("Net.Stim.Out", "Stim Out");
        AppendInfo("Net.Stim.Sense", "Stim Sense");
        AppendInfo("Net.Gnd.Out", "GND Out");
        AppendInfo("Net.Gnd.Sense", "GND Sense");
        AppendInfo("Scanner", "Scanner");
        AppendInfo("Wires", "Wires");

        var additional = infoParts.Count > 0 ? string.Join(", ", infoParts) : null;
        var destination = Clean(reference);
        var expression = Clean(measurement);

        return new TestRecordMetricViewModel(
            Clean(title) ?? "ICT Komponente",
            destination,
            expression,
            measurementUnit,
            type,
            additional,
            lower,
            upper);
    }

    private static void AppendArbDetails(string filePath, TestDetailsViewModel viewModel)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var orderedEntries = new List<KeyValuePair<string, string>>();
        foreach (var rawLine in File.ReadAllLines(filePath, AnsiEncoding))
        {
            var line = StripComments(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var (key, value) = ParseKeyValue(line);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            orderedEntries.Add(new KeyValuePair<string, string>(key!, value));
            values[key!] = value;
        }

        if (values.TryGetValue("Name", out var name))
        {
            viewModel.Message ??= Clean(name);
        }

        var sampleTime = values.TryGetValue("ArbSampleTime", out var sampleText) &&
                         TryParseMeasurement(sampleText, out var sampleSeconds, out _)
            ? sampleSeconds
            : 1.0;

        var samples = new List<Point>();
        var index = 0;
        while (true)
        {
            var key = $"ArbSample0:{index}";
            if (!values.TryGetValue(key, out var valueText))
            {
                break;
            }

            if (TryParseMeasurement(valueText, out var value, out var unit))
            {
                var time = index * sampleTime;
                samples.Add(new Point(time, value));
                viewModel.Description ??= $"Arbiträrsignal ({unit})";
            }

            index++;
        }

        if (samples.Count > 0)
        {
            viewModel.SetWaveform(samples, "V");
        }

        AppendArbMetadataRecords(orderedEntries, viewModel);
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

    private static bool TryParseMeasurement(string? text, out double value, out string unit)
    {
        value = 0;
        unit = string.Empty;
        var cleaned = Clean(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        cleaned = cleaned.Replace("æ", "µ");
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

    private static double ExtractPrefix(string unit, out string normalizedUnit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            normalizedUnit = unit;
            return 1;
        }

        unit = unit.Trim();
        var prefixes = new Dictionary<char, double>
        {
            { 'p', 1e-12 }, { 'n', 1e-9 }, { 'u', 1e-6 }, { 'µ', 1e-6 }, { 'm', 1e-3 },
            { 'k', 1e3 }, { 'K', 1e3 }, { 'M', 1e6 }, { 'G', 1e9 }
        };

        var first = unit[0];
        if (prefixes.TryGetValue(first, out var multiplier) && unit.Length > 1 && char.IsLetter(unit[1]))
        {
            normalizedUnit = unit[1..];
            return multiplier;
        }

        normalizedUnit = unit;
        return 1;
    }

    private static (double? LowerFactor, double? UpperFactor) ParseTolerancePercent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        raw = raw.Replace("ñ", "±");
        if (raw.StartsWith("±", StringComparison.Ordinal))
        {
            var percent = ParseSinglePercent(raw[1..]);
            return (-percent, percent);
        }

        if (raw.Contains('/'))
        {
            var parts = raw.Split('/');
            var lower = ParseSinglePercent(parts[0]);
            var upper = ParseSinglePercent(parts.Length > 1 ? parts[1] : parts[0]);
            return (lower, upper);
        }

        var value = ParseSinglePercent(raw);
        return (value, value);
    }

    private static double ParseSinglePercent(string text)
    {
        var cleaned = text.Replace("%", string.Empty).Replace("+", string.Empty).Trim();
        double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var number);
        return number / 100.0;
    }

    private static void AppendChannelDetails(TestParameters parameters, TestDetailsViewModel viewModel)
    {
        var stimulusChannels = new[]
        {
            ("Stimulus 1", parameters.StimulusChannel1),
            ("Stimulus 2", parameters.StimulusChannel2)
        };

        foreach (var (title, channel) in stimulusChannels)
        {
            if (channel == null)
            {
                continue;
            }

            var info = new List<string>();
            if (!string.IsNullOrWhiteSpace(channel.Mode)) info.Add(channel.Mode);
            if (!string.IsNullOrWhiteSpace(channel.Current)) info.Add($"I={channel.Current}");
            if (!string.IsNullOrWhiteSpace(channel.Voltage)) info.Add($"U={channel.Voltage}");
            if (!string.IsNullOrWhiteSpace(channel.VoltageLimit)) info.Add($"Ulim={channel.VoltageLimit}");

            viewModel.Records.Add(new TestRecordMetricViewModel(
                title,
                channel.Target ?? channel.Source,
                channel.Source,
                null,
                "Stimulus",
                info.Count > 0 ? string.Join(", ", info) : null,
                null,
                null));
        }

        var acquisitionChannels = new[]
        {
            ("Messkanal 1", parameters.AcquisitionChannel1),
            ("Messkanal 2", parameters.AcquisitionChannel2),
            ("Messkanal 3", parameters.AcquisitionChannel3)
        };

        foreach (var (title, channel) in acquisitionChannels)
        {
            if (channel == null)
            {
                continue;
            }

            var info = new List<string>();
            if (!string.IsNullOrWhiteSpace(channel.Range)) info.Add($"Range={channel.Range}");
            if (!string.IsNullOrWhiteSpace(channel.Filter)) info.Add($"Filter={channel.Filter}");

            viewModel.Records.Add(new TestRecordMetricViewModel(
                title,
                channel.Source,
                channel.Source,
                null,
                "Acq",
                info.Count > 0 ? string.Join(", ", info) : null,
                null,
                null));
        }
    }

    private static void AppendAdditionalAttributes(TestParameters parameters, TestDetailsViewModel viewModel)
    {
        if (parameters.AdditionalAttributes == null || parameters.AdditionalAttributes.Length == 0)
        {
            return;
        }

        foreach (var attribute in parameters.AdditionalAttributes)
        {
            if (attribute == null)
            {
                continue;
            }

            var name = attribute.LocalName;
            if (string.IsNullOrWhiteSpace(name) ||
                IgnoredParameterAttributeNames.Contains(name) ||
                IsDisplayOptionAttribute(name))
            {
                continue;
            }

            var value = Clean(attribute.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            viewModel.Records.Add(new TestRecordMetricViewModel(
                name,
                null,
                value,
                null,
                "Parameter",
                null,
                null,
                null));
        }
    }

    private static void AppendDisplayOptions(TestParameters parameters, TestDetailsViewModel viewModel)
    {
        if (parameters.AdditionalAttributes == null || parameters.AdditionalAttributes.Length == 0)
        {
            return;
        }

        foreach (var attribute in parameters.AdditionalAttributes)
        {
            if (attribute == null)
            {
                continue;
            }

            var name = attribute.LocalName;
            if (string.IsNullOrWhiteSpace(name) || !IsDisplayOptionAttribute(name))
            {
                continue;
            }

            var value = Clean(attribute.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            viewModel.DisplayOptions.Add(new DisplayOptionViewModel(
                FormatDisplayOptionLabel(name),
                value));
        }
    }

    private static void AppendArbMetadataRecords(IEnumerable<KeyValuePair<string, string>> entries, TestDetailsViewModel viewModel)
    {
        foreach (var entry in entries)
        {
            if (ArbMetadataIgnoredKeys.Contains(entry.Key))
            {
                continue;
            }

            if (ArbMetadataIgnoredPrefixes.Any(prefix => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var value = Clean(entry.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            viewModel.Records.Add(new TestRecordMetricViewModel(
                entry.Key,
                null,
                value,
                null,
                "AM2",
                null,
                null,
                null));
        }
    }

    private static bool TryResolveExternalFile(string? relativePath, string? baseDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var sanitized = relativePath.Trim().Trim('\'', '"').Replace("/", "\\");
        var combined = string.IsNullOrWhiteSpace(baseDirectory) ? sanitized : Path.Combine(baseDirectory, sanitized);
        if (!File.Exists(combined))
        {
            return false;
        }

        resolvedPath = combined;
        return true;
    }

    private static bool IsAm2Test(Test test)
    {
        if (test.Id != null && test.Id.StartsWith("AM", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var reference = test.Parameters?.DrawingReference ?? string.Empty;
        return reference.IndexOf("AM2", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsIctTest(Test test)
    {
        if (test.Id != null && test.Id.StartsWith("ICT", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var name = test.Parameters?.Name ?? string.Empty;
        return name.IndexOf("ICT", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDisplayOptionAttribute(string name)
    {
        if (DisplayOptionAttributeNames.Contains(name))
        {
            return true;
        }

        return name.StartsWith("Show", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDisplayOptionLabel(string name)
    {
        return name switch
        {
            "ShowOK" => "Show OK",
            "ShowAbort" => "Show Abort",
            "ShowHotKeys" => "Show HotKeys",
            _ => name
        };
    }
}
