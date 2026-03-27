// Provides Scenario Preset Store for the desktop application configuration support.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ct3xxSimulator.Desktop.Configuration;

/// <summary>
/// Represents the scenario preset store.
/// </summary>
public sealed class ScenarioPresetStore
{
    private readonly string _path;

    /// <summary>
    /// Gets the default path.
    /// </summary>
    public static string GetDefaultPath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ct3xxSimulatorDesktop",
            "scenarios.json");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioPresetStore"/> class.
    /// </summary>
    public ScenarioPresetStore(string? path = null)
    {
        _path = path ?? GetDefaultPath();
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => _path;

    /// <summary>
    /// Executes load.
    /// </summary>
    public IReadOnlyList<ScenarioPreset> Load()
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<ScenarioPreset>();
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<ScenarioPreset>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<ScenarioPreset>();
    }

    /// <summary>
    /// Executes save.
    /// </summary>
    public void Save(IEnumerable<ScenarioPreset> presets)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(
            presets.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
