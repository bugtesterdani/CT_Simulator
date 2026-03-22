using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Simulation.WireViz;

internal static class WireVizFileLocator
{
    public static IReadOnlyList<string> FindCandidateFiles(string programDirectory)
    {
        if (string.IsNullOrWhiteSpace(programDirectory) || !Directory.Exists(programDirectory))
        {
            return Array.Empty<string>();
        }

        var candidates = Directory.EnumerateFiles(programDirectory, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(programDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Rank(Path.GetFileName(path)))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return candidates;
        }

        var parser = new WireVizParser();
        var valid = new List<string>();
        foreach (var candidate in candidates)
        {
            try
            {
                var document = parser.ParseFile(candidate);
                if (document.Connectors.Count > 0 && document.Connections.Count > 0)
                {
                    valid.Add(candidate);
                }
            }
            catch
            {
                // Ignore unrelated YAML files.
            }
        }

        return valid;
    }

    private static int Rank(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return 1000;
        }

        if (fileName.Equals("wireviz.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("wireviz.yml", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.Contains("wireviz", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 10;
    }
}
