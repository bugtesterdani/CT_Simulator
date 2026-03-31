// Provides Wire Viz File Locator for the module runtime simulation support.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Simulation.WireViz;

internal static class WireVizFileLocator
{
    /// <summary>
    /// Finds the candidate files.
    /// </summary>
    public static IReadOnlyList<string> FindCandidateFiles(string programDirectory)
    {
        var overrideRoot = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot) && Directory.Exists(overrideRoot))
        {
            return FindCandidateFilesInRoot(Path.GetFullPath(overrideRoot));
        }

        if (string.IsNullOrWhiteSpace(programDirectory) || !Directory.Exists(programDirectory))
        {
            return Array.Empty<string>();
        }

        return FindCandidateFilesInRoots(EnumerateSearchRoots(programDirectory));
    }

    /// <summary>
    /// Executes FindCandidateFilesInRoot.
    /// </summary>
    private static IReadOnlyList<string> FindCandidateFilesInRoot(string rootDirectory)
    {
        return FindCandidateFilesInRoots(new[] { rootDirectory });
    }

    /// <summary>
    /// Executes FindCandidateFilesInRoots.
    /// </summary>
    private static IReadOnlyList<string> FindCandidateFilesInRoots(IEnumerable<string> roots)
    {
        var candidates = roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.yml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)))
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

    /// <summary>
    /// Executes EnumerateSearchRoots.
    /// </summary>
    private static IEnumerable<string> EnumerateSearchRoots(string programDirectory)
    {
        yield return programDirectory;

        var parent = Directory.GetParent(programDirectory);
        if (parent == null)
        {
            yield break;
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(parent.FullName, "*", SearchOption.AllDirectories))
        {
            yield return subDirectory;
        }
    }

    /// <summary>
    /// Executes Rank.
    /// </summary>
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
