// Provides Altium Connectivity Csv Reader for the Altium exporter Altium import support.
using System.Text;
using Ct3xxAltiumWireVizExporter.Configuration;

namespace Ct3xxAltiumWireVizExporter.Altium;

/// <summary>
/// Represents the altium connectivity csv reader.
/// </summary>
public sealed class AltiumConnectivityCsvReader
{
    /// <summary>
    /// Executes read.
    /// </summary>
    public IReadOnlyList<AltiumConnectivityRecord> Read(string path, ExportConfiguration configuration)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Connectivity CSV not found.", path);
        }

        var lines = File.ReadAllLines(path, Encoding.UTF8)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count == 0)
        {
            return Array.Empty<AltiumConnectivityRecord>();
        }

        var header = ParseCsvLine(lines[0]);
        var map = BuildHeaderMap(header);
        RequireColumn(map, "net");
        RequireColumn(map, "designator");
        RequireColumn(map, "pin");

        var records = new List<AltiumConnectivityRecord>();
        for (var index = 1; index < lines.Count; index++)
        {
            var values = ParseCsvLine(lines[index]);
            var netName = ReadValue(values, map, "net");
            var designator = ReadValue(values, map, "designator");
            var pin = ReadValue(values, map, "pin");
            var componentKind = ReadValue(values, map, "componentkind");
            var pinName = ReadValue(values, map, "pinname");

            if (string.IsNullOrWhiteSpace(netName) ||
                string.IsNullOrWhiteSpace(designator) ||
                string.IsNullOrWhiteSpace(pin))
            {
                continue;
            }

            if (!configuration.ShouldIncludeNet(netName) || !configuration.IsConnector(designator, componentKind))
            {
                continue;
            }

            records.Add(new AltiumConnectivityRecord(
                netName.Trim(),
                designator.Trim(),
                pin.Trim(),
                configuration.ResolveConnectorName(designator),
                configuration.ResolveRole(designator),
                string.IsNullOrWhiteSpace(pinName) ? null : pinName.Trim()));
        }

        return records;
    }

    /// <summary>
    /// Executes BuildHeaderMap.
    /// </summary>
    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            var normalized = header[index].Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
            if (!map.ContainsKey(normalized))
            {
                map[normalized] = index;
            }
        }

        return map;
    }

    /// <summary>
    /// Executes RequireColumn.
    /// </summary>
    private static void RequireColumn(IReadOnlyDictionary<string, int> map, string columnName)
    {
        if (!map.ContainsKey(columnName))
        {
            throw new InvalidOperationException($"CSV column '{columnName}' is required.");
        }
    }

    /// <summary>
    /// Executes ReadValue.
    /// </summary>
    private static string? ReadValue(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> map, string key)
    {
        if (!map.TryGetValue(key, out var index))
        {
            return null;
        }

        return index < values.Count ? values[index] : null;
    }

    /// <summary>
    /// Executes ParseCsvLine.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }
}
