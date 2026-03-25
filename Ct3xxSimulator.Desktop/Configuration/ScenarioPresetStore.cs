using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ct3xxSimulator.Desktop.Configuration;

public sealed class ScenarioPresetStore
{
    private readonly string _path;

    public static string GetDefaultPath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ct3xxSimulatorDesktop",
            "scenarios.json");
    }

    public ScenarioPresetStore(string? path = null)
    {
        _path = path ?? GetDefaultPath();
    }

    public string FilePath => _path;

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
