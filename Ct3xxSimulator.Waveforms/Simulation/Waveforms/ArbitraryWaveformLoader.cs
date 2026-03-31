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

/// <summary>
/// Loads CT3xx arbitrary waveform files and converts them into normalized waveform objects.
/// </summary>
public static class ArbitraryWaveformLoader
{
    private static readonly Regex ParameterRegex = new(@"^\s*(?<key>[^\s{]+)\s+\{""(?<value>.*)""\}\s*$", RegexOptions.Compiled);
    private static readonly Regex SampleRegex = new(@"^ArbSample(?<channel>\d+):(?<index>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Loads all waveform channels referenced by one CT3xx test.
    /// </summary>
    /// <param name="fileSet">The loaded CT3xx program file set.</param>
    /// <param name="test">The test that references the waveform file.</param>
    /// <param name="waveforms">When successful, receives the loaded waveform channels.</param>
    /// <param name="error">When loading fails, receives the error description.</param>
    /// <returns><see langword="true"/> when at least one waveform channel was loaded.</returns>
    public static bool TryLoadAll(Ct3xxProgramFileSet? fileSet, Test test, out IReadOnlyList<AppliedWaveform> waveforms, out string? error)
    {
        waveforms = Array.Empty<AppliedWaveform>();
        error = null;

        if (!TryLoadParameters(fileSet, test, out var parameters, out error))
        {
            return false;
        }

        var sampleTimeMs = ParseEngineeringValue(parameters.TryGetValue("ArbSampleTime", out var sampleTimeText) ? sampleTimeText : null, "ms") ?? 0d;
        var delayMs = ParseEngineeringValue(parameters.TryGetValue("ArbDelay", out var delayText) ? delayText : null, "ms") ?? 0d;
        var burstText = parameters.TryGetValue("BurstCount", out var burstValue) ? burstValue : null;
        var periodic = (parameters.TryGetValue("ArbMode", out var modeText) &&
                       modeText.Contains("periodic", StringComparison.OrdinalIgnoreCase)) ||
                       IsUntilTestEnd(burstText);
        var cycles = ParseCycles(burstText);
        var waveformName = parameters.TryGetValue("Name", out var name) ? name : test.Name ?? test.Id ?? "Waveform";
        var channelCount = ParseChannelCount(parameters);

        var items = new List<AppliedWaveform>();
        for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            var points = ParsePoints(parameters, sampleTimeMs, channelIndex);
            if (points.Count == 0)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
            {
                ["CHANNEL_INDEX"] = channelIndex.ToString(CultureInfo.InvariantCulture),
                ["CHANNEL_CARD_INDEX"] = (channelIndex + 1).ToString(CultureInfo.InvariantCulture)
            };
            var signalName = ResolveSignalName(parameters, channelIndex);
            items.Add(new AppliedWaveform(signalName, $"{waveformName} CH{channelIndex + 1}", points, sampleTimeMs, delayMs, periodic, cycles, metadata));
        }

        if (items.Count == 0)
        {
            error = "Waveform-Datei enthaelt keine verwertbaren ARB-Kanaele.";
            return false;
        }

        waveforms = items;
        return true;
    }

    /// <summary>
    /// Loads the first waveform channel referenced by one CT3xx test.
    /// </summary>
    /// <param name="fileSet">The loaded CT3xx program file set.</param>
    /// <param name="test">The test that references the waveform file.</param>
    /// <param name="waveform">When successful, receives the first loaded waveform channel.</param>
    /// <param name="error">When loading fails, receives the error description.</param>
    /// <returns><see langword="true"/> when a waveform channel was loaded.</returns>
    public static bool TryLoad(Ct3xxProgramFileSet? fileSet, Test test, out AppliedWaveform waveform, out string? error)
    {
        waveform = null!;
        error = null;

        if (!TryLoadAll(fileSet, test, out var waveforms, out error))
        {
            return false;
        }

        waveform = waveforms[0];
        return true;
    }

    /// <summary>
    /// Executes TryLoadParameters.
    /// </summary>
    private static bool TryLoadParameters(Ct3xxProgramFileSet? fileSet, Test test, out Dictionary<string, string> parameters, out string? error)
    {
        parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        parameters = ParseParameters(document.Lines);
        if (parameters.Count == 0)
        {
            error = $"Waveform-Datei '{relativeFile}' konnte nicht gelesen werden.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Executes ParseParameters.
    /// </summary>
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

    /// <summary>
    /// Executes ParsePoints.
    /// </summary>
    private static IReadOnlyList<WaveformPoint> ParsePoints(IReadOnlyDictionary<string, string> parameters, double sampleTimeMs, int channelIndex)
    {
        var samples = new SortedDictionary<int, double>();
        foreach (var item in parameters)
        {
            var match = SampleRegex.Match(item.Key);
            if (!match.Success || match.Groups["channel"].Value != channelIndex.ToString(CultureInfo.InvariantCulture))
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

    /// <summary>
    /// Executes ResolveSignalName.
    /// </summary>
    private static string ResolveSignalName(IReadOnlyDictionary<string, string> parameters, int channelIndex)
    {
        if (parameters.TryGetValue("TP_ARB", out var testPoint) && !string.IsNullOrWhiteSpace(testPoint))
        {
            return $"AM2/{channelIndex + 1} {testPoint.Trim()}";
        }

        if (parameters.TryGetValue("MBUS_ARB", out var mbus) && !string.IsNullOrWhiteSpace(mbus))
        {
            return $"AM2/{channelIndex + 1} MBus{mbus.Trim()}";
        }

        return $"AM2/{channelIndex + 1} ARB";
    }

    /// <summary>
    /// Executes ParseChannelCount.
    /// </summary>
    private static int ParseChannelCount(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("Channels", out var channelText) &&
            int.TryParse(Regex.Match(channelText, @"\d+").Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return 1;
    }

    /// <summary>
    /// Executes ParseCycles.
    /// </summary>
    private static int ParseCycles(string? burstText)
    {
        if (string.IsNullOrWhiteSpace(burstText))
        {
            return 1;
        }

        if (IsUntilTestEnd(burstText))
        {
            return 1;
        }

        var match = Regex.Match(burstText, @"[-+]?\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles)
            ? Math.Max(cycles, 1)
            : 1;
    }

    /// <summary>
    /// Executes IsUntilTestEnd.
    /// </summary>
    private static bool IsUntilTestEnd(string? burstText)
    {
        return !string.IsNullOrWhiteSpace(burstText) &&
               burstText.Contains("until", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a CT3xx engineering value into the simulator's base unit representation.
    /// </summary>
    /// <param name="text">The engineering value text to parse.</param>
    /// <param name="defaultUnit">The unit to assume when <paramref name="text"/> does not contain one.</param>
    /// <returns>The parsed numeric value in base units, or <see langword="null"/> when parsing failed.</returns>
    public static double? ParseEngineeringValue(string? text, string defaultUnit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text.Trim(), @"^\s*([-+]?\d+(?:[.,]\d+)?)\s*([A-Za-zµ<>/ ]*)");
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
