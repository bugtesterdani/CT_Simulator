using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Ct3xxSimulator.Desktop.Configuration;
using Ct3xxSimulator.Validation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _scenarioPresetStore = new ScenarioPresetStore(ScenarioPresetFilePath);
        LoadScenarioPresets();
        ApplyDefaultScenario();
        UpdateConfigurationSummary();
        ValidateCurrentConfiguration(false);
    }

    private void ApplyDefaultScenario()
    {
        var simtestRoot = FindSimtestRoot();
        if (simtestRoot == null)
        {
            ConfigurationSummary = "Keine Standardkonfiguration erkannt.";
            return;
        }

        TestProgramFolderPath = Path.Combine(simtestRoot, "ct3xx");
        WiringFolderPath = Path.Combine(simtestRoot, "wireplan");
        SimulationModelFolderPath = Path.Combine(simtestRoot, "wireplan");
        PythonScriptPath = Path.Combine(simtestRoot, "device", "devices", "IKI_good.json");

        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath, false);
        }
    }

    private static string? FindSimtestRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "simtest"),
            Path.Combine(AppContext.BaseDirectory, "simtest"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "simtest")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private void OnBrowseProgramFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit CT3xx-Testprogramm waehlen" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        TestProgramFolderPath = dialog.FolderName;
        ResolveProgramFromCurrentFolder(true);
        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath, true);
        }
    }

    private void OnBrowseWiringFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit WireViz-Verdrahtung waehlen" };
        if (dialog.ShowDialog() == true)
        {
            WiringFolderPath = dialog.FolderName;
        }
    }

    private void OnBrowseSimulationModelFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit Simulationsmodell waehlen" };
        if (dialog.ShowDialog() == true)
        {
            SimulationModelFolderPath = dialog.FolderName;
        }
    }

    private void OnBrowsePythonScript(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Geraetemodell oder Python-Skript waehlen",
            Filter = "Geraetemodelle (*.py;*.json;*.yaml;*.yml)|*.py;*.json;*.yaml;*.yml|Python (*.py)|*.py|JSON (*.json)|*.json|YAML (*.yaml;*.yml)|*.yaml;*.yml|Alle Dateien (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PythonScriptPath = dialog.FileName;
        }
    }

    private void OnLoadProgramFromResolvedSelection(object sender, RoutedEventArgs e)
    {
        ResolveProgramFromCurrentFolder(true);
        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath);
        }
    }

    private void ResolveProgramFromCurrentFolder(bool promptIfMultiple)
    {
        SelectedFilePath = ResolveProgramFileFromFolder(TestProgramFolderPath, promptIfMultiple);
        UpdateConfigurationSummary();
    }

    private string? ResolveProgramFileFromFolder(string? folderPath, bool promptIfMultiple)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return null;
        }

        var programs = Directory.GetFiles(folderPath, "*.ctxprg", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (programs.Count == 0)
        {
            AddLog($"Im Ordner '{folderPath}' wurde keine .ctxprg gefunden.");
            return null;
        }

        if (programs.Count == 1 || !promptIfMultiple)
        {
            return programs[0];
        }

        var choice = PromptSelection("Mehrere CT3xx-Programme gefunden. Bitte waehlen:", programs);
        return string.IsNullOrWhiteSpace(choice) ? programs[0] : choice;
    }

    private bool LoadProgramFile(string filePath, bool showErrors = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            _fileSet = _fileParser.Load(fullPath);
            _program = _fileSet.Program;
            SelectedFilePath = fullPath;
            StepResults.Clear();
            Logs.Clear();
            CurrentStep = null;
            AddLog($"Programm geladen: {Path.GetFileName(fullPath)}");
            AddLog($"Externe Dateien: {_fileSet.ExternalFiles.Count}");
            UpdateConfigurationSummary();
            return true;
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "Fehler beim Laden", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            AddLog($"Fehler beim Laden: {ex.Message}");
            return false;
        }
    }

    private void UpdateConfigurationSummary()
    {
        ConfigurationSummary =
            $"Programm: {Path.GetFileName(SelectedFilePath ?? string.Empty)} | " +
            $"Verdrahtung: {Path.GetFileName(WiringFolderPath ?? string.Empty)} | " +
            $"Simulation: {Path.GetFileName(SimulationModelFolderPath ?? string.Empty)} | " +
            $"Geraetemodell: {Path.GetFileName(PythonScriptPath ?? string.Empty)} | " +
            $"Szenario-Datei: {Path.GetFileName(ScenarioPresetFilePath ?? string.Empty)}";
    }

    private void LoadScenarioPresets()
    {
        ScenarioPresets.Clear();
        foreach (var preset in _scenarioPresetStore.Load().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            ScenarioPresets.Add(preset);
        }

        AddLog($"Szenario-Datei geladen: {_scenarioPresetStore.FilePath}");
    }

    private void OnBrowseScenarioPresetFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Szenario-Datei waehlen",
            Filter = "JSON (*.json)|*.json|Alle Dateien (*.*)|*.*",
            FileName = Path.GetFileName(ScenarioPresetFilePath ?? "scenarios.json"),
            InitialDirectory = GetInitialDirectory(ScenarioPresetFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            ScenarioPresetFilePath = dialog.FileName;
        }
    }

    private void OnLoadScenarioPresetFile(object sender, RoutedEventArgs e)
    {
        _scenarioPresetStore = new ScenarioPresetStore(ScenarioPresetFilePath);
        LoadScenarioPresets();
        AddLog($"Szenario-Datei explizit geladen: {_scenarioPresetStore.FilePath}");
    }

    private void OnSaveScenarioPresetFileAs(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Szenario-Datei speichern unter",
            Filter = "JSON (*.json)|*.json|Alle Dateien (*.*)|*.*",
            FileName = Path.GetFileName(ScenarioPresetFilePath ?? "scenarios.json"),
            InitialDirectory = GetInitialDirectory(ScenarioPresetFilePath)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ScenarioPresetFilePath = dialog.FileName;
        _scenarioPresetStore = new ScenarioPresetStore(ScenarioPresetFilePath);
        _scenarioPresetStore.Save(ScenarioPresets);
        AddLog($"Szenario-Datei gespeichert: {_scenarioPresetStore.FilePath}");
    }

    private void OnApplyScenarioPreset(object sender, RoutedEventArgs e)
    {
        if (SelectedScenarioPreset == null)
        {
            return;
        }

        TestProgramFolderPath = SelectedScenarioPreset.TestProgramFolderPath;
        WiringFolderPath = SelectedScenarioPreset.WiringFolderPath;
        SimulationModelFolderPath = SelectedScenarioPreset.SimulationModelFolderPath;
        PythonScriptPath = SelectedScenarioPreset.PythonScriptPath;
        ResolveProgramFromCurrentFolder(false);
        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath, false);
        }

        AddLog($"Szenario geladen: {SelectedScenarioPreset.Name}");
    }

    private void OnSaveScenarioPreset(object sender, RoutedEventArgs e)
    {
        var name = PromptInput("Name fuer das Szenario:");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existing = ScenarioPresets.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ScenarioPresets.Remove(existing);
        }

        var preset = new ScenarioPreset
        {
            Name = name.Trim(),
            TestProgramFolderPath = TestProgramFolderPath,
            WiringFolderPath = WiringFolderPath,
            SimulationModelFolderPath = SimulationModelFolderPath,
            PythonScriptPath = PythonScriptPath
        };

        ScenarioPresets.Add(preset);
        SelectedScenarioPreset = preset;
        _scenarioPresetStore.Save(ScenarioPresets);
        AddLog($"Szenario gespeichert: {preset.Name}");
    }

    private void OnDeleteScenarioPreset(object sender, RoutedEventArgs e)
    {
        if (SelectedScenarioPreset == null)
        {
            return;
        }

        var removedName = SelectedScenarioPreset.Name;
        ScenarioPresets.Remove(SelectedScenarioPreset);
        SelectedScenarioPreset = null;
        _scenarioPresetStore.Save(ScenarioPresets);
        AddLog($"Szenario geloescht: {removedName}");
    }

    private void OnValidateConfiguration(object sender, RoutedEventArgs e)
    {
        var issues = ValidateCurrentConfiguration(true);
        if (issues.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, issues), "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private IReadOnlyList<string> ValidateCurrentConfiguration(bool showSuccessMessage)
    {
        var issues = SimulationConfigurationValidator.Validate(SelectedFilePath, WiringFolderPath, SimulationModelFolderPath, PythonScriptPath);
        ValidationSummary = issues.Count == 0 ? "Validierung: OK" : $"Validierung: {issues.Count} Problem(e)";
        if (showSuccessMessage && issues.Count == 0)
        {
            MessageBox.Show(this, "Konfiguration ist gueltig.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return issues;
    }

    private static string? GetInitialDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}
