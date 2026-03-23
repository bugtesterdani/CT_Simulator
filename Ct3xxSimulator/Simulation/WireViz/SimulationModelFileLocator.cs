using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ct3xxSimulator.Simulation.WireViz;

internal static class SimulationModelFileLocator
{
    public static string? FindCandidateFile(string programDirectory)
    {
        var overrideRoot = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot) && Directory.Exists(overrideRoot))
        {
            return Directory.EnumerateFiles(Path.GetFullPath(overrideRoot), "*.y*ml", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path =>
                {
                    var name = Path.GetFileName(path);
                    return name.Equals("simulation.yaml", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("simulation.yml", StringComparison.OrdinalIgnoreCase);
                });
        }

        if (string.IsNullOrWhiteSpace(programDirectory) || !Directory.Exists(programDirectory))
        {
            return null;
        }

        var candidates = EnumerateSearchRoots(programDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.yml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)))
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Equals("simulation.yaml", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals("simulation.yml", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return candidates;
    }

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
}
