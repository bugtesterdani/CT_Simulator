using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Programs;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation.Waveforms;

internal static class ArbitraryWaveformLoader
{
    private static readonly Regex ParameterRegex = new(@"^\s*(?<key>[^\s{]+)\s+\{""(?<value>.*)""\}\s*$", RegexOptions.Compiled);
    private static readonly Regex SampleRegex = new(@"^ArbSample(?<channel>\d+):(?<index>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryLoad(Ct3xxProgramFileSet? fileSet, Test test, out AppliedWaveform waveform, out string? error)
    {
        waveform = null!;
        error = null;

        if (fileSet == null)
        {
            error = "Programmdateien nicht geladen.";
            return false;
        }

        var relativeFile = test.File?.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(relativeFile))
        {
            error = "Waveform-Test ohne Dateiverweis.";
            return false;
        }

        var fullPath = Path.GetFullPath(Path.Combine(fileSet.ProgramDirectory, relativeFile));
        var document = fileSet.GetDocuments<ArbitraryWaveformDocument>()
            .FirstOrDefault(item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

        if (document == null && File.Exists(fullPath))
        {
            document = new ArbitraryWaveformDocument(fullPath, test.Parameters?.Tables.FirstOrDefault(), File.ReadAllLines(fullPath));
        }

        if (document == null)
        {
            error = $"Waveform-Datei '{relativeFile}' nicht gefunden.";
            return false;
        }

        var parameters = ParseParameters(document.Lines);
        if (parameters.Count == 0)
        {
            error = $"Waveform-Datei '{relativeFile}' konnte nicht gelesen werden.";
            return false;
        }

        var sampleTimeMs = ParseEngineeringValue(parameters.TryGetValue("ArbSampleTime", out var sampleTimeText) ? sampleTimeText : null, "ms") ?? 0d;
        var delayMs = ParseEngineeringValue(parameters.TryGetValue("ArbDelay", out var delayText) ? delayText : null, "ms") ?? 0d;
        var periodic = parameters.TryGetValue("ArbMode", out var modeText) &&
                       modeText.Contains("periodic", StringComparison.OrdinalIgnoreCase);
        var cycles = ParseCycles(parameters.TryGetValue("BurstCount", out var burstText) ? burstText : null);
        var points = ParsePoints(parameters, sampleTimeMs);
        var signalName = ResolveSignalName(parameters);
        var waveformName = parameters.TryGetValue("Name", out var name) ? name : test.Name ?? test.Id ?? "Waveform";

        waveform = new AppliedWaveform(signalName, waveformName, points, sampleTimeMs, delayMs, periodic, cycles, parameters);
        return true;
    }

    private static Dictionary<string, string> ParseParameters(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inParameters = false;
        var depth = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!inParameters)
            {
                if (string.Equals(line, "parameters", StringComparison.OrdinalIgnoreCase))
                {
                    inParameters = true;
                }

                continue;
            }

            if (line == "{")
            {
                depth++;
                continue;
            }

            if (line == "}")
            {
                depth--;
                if (depth <= 0)
                {
                    break;
                }

                continue;
            }

            var match = ParameterRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            result[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static IReadOnlyList<WaveformPoint> ParsePoints(IReadOnlyDictionary<string, string> parameters, double sampleTimeMs)
    {
        var samples = new SortedDictionary<int, double>();
        foreach (var item in parameters)
        {
            var match = SampleRegex.Match(item.Key);
            if (!match.Success || match.Groups["channel"].Value != "0")
            {
                continue;
            }

            if (!int.TryParse(match.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var value = ParseEngineeringValue(item.Value, "V");
            if (!value.HasValue)
            {
                continue;
            }

            samples[index] = value.Value;
        }

        if (samples.Count == 0)
        {
            return Array.Empty<WaveformPoint>();
        }

        var fallbackTimeMs = sampleTimeMs <= 0 ? 1d : sampleTimeMs;
        return samples
            .Select(item => new WaveformPoint(item.Key * fallbackTimeMs, item.Value))
            .ToList();
    }

    private static string ResolveSignalName(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("TP_ARB", out var testPoint) && !string.IsNullOrWhiteSpace(testPoint))
        {
            return testPoint.Trim();
        }

        if (parameters.TryGetValue("MBUS_ARB", out var mbus) && !string.IsNullOrWhiteSpace(mbus))
        {
            return $"MBus{mbus.Trim()}";
        }

        return "ARB";
    }

    private static int ParseCycles(string? burstText)
    {
        if (string.IsNullOrWhiteSpace(burstText))
        {
            return 1;
        }

        if (burstText.Contains("until", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var match = Regex.Match(burstText, @"[-+]?\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            ? Math.Max(cycles, 1)
            : 1;
    }

    internal static double? ParseEngineeringValue(string? text, string defaultUnit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text.Trim(), @"^\s*([-+]?\d+(?:[.,]\d+)?)\s*([A-Za-zÂµ<>/ ]*)");
        if (!match.Success)
        {
            return null;
        }

        var numericText = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var unit = match.Groups[2].Value.Trim();
        if (string.IsNullOrWhiteSpace(unit))
        {
            unit = defaultUnit;
        }

        return unit.ToLowerInvariant() switch
        {
            "v" => value,
            "mv" => value / 1000d,
            "a" => value,
            "ma" => value / 1000d,
            "s" => value * 1000d,
            "ms" => value,
            "us" => value / 1000d,
            "ns" => value / 1_000_000d,
            _ => value
        };
    }
}
