// Provides Simulation Configuration Validator for the validation layer validation support.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ct3xxSimulator.Validation;

public static class SimulationConfigurationValidator
{
    /// <summary>
    /// Executes validate.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        string? selectedFilePath,
        string? wiringFolderPath,
        string? simulationModelFolderPath,
        string? pythonScriptPath)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(selectedFilePath) || !File.Exists(selectedFilePath))
        {
            issues.Add("Das Testprogramm wurde nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(wiringFolderPath) || !Directory.Exists(wiringFolderPath))
        {
            issues.Add("Der Verdrahtungs-Ordner existiert nicht.");
        }
        else if (!Directory.EnumerateFiles(wiringFolderPath, "*.yml", SearchOption.TopDirectoryOnly)
                     .Concat(Directory.EnumerateFiles(wiringFolderPath, "*.yaml", SearchOption.TopDirectoryOnly))
                     .Any())
        {
            issues.Add("Im Verdrahtungs-Ordner wurde keine YAML-Datei gefunden.");
        }

        if (string.IsNullOrWhiteSpace(simulationModelFolderPath) || !Directory.Exists(simulationModelFolderPath))
        {
            issues.Add("Der Simulations-Ordner existiert nicht.");
        }
        else if (!Directory.EnumerateFiles(simulationModelFolderPath, "simulation.y*ml", SearchOption.TopDirectoryOnly).Any())
        {
            issues.Add("Im Simulations-Ordner wurde keine simulation.yaml gefunden.");
        }

        if (string.IsNullOrWhiteSpace(pythonScriptPath) || !File.Exists(pythonScriptPath))
        {
            issues.Add("Das Geraetemodell wurde nicht gefunden.");
        }
        else if (!IsSupportedDeviceModelExtension(Path.GetExtension(pythonScriptPath)))
        {
            issues.Add("Erlaubte Geraetemodell-Typen sind .py, .json, .yaml und .yml.");
        }

        issues.AddRange(SimulationModelDeepValidator.Validate(wiringFolderPath, simulationModelFolderPath));
        return issues;
    }

    /// <summary>
    /// Executes IsSupportedDeviceModelExtension.
    /// </summary>
    private static bool IsSupportedDeviceModelExtension(string extension)
    {
        return extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}
