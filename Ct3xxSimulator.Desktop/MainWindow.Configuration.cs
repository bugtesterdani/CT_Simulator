// Provides Main Window Configuration for the desktop application support code.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using Ct3xxSimulator.Desktop.Configuration;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
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
            StepTreeRootNodes.Clear();
            _stepEvaluationHistory.Clear();
            Logs.Clear();
            CurrentStep = null;
            _isLoadedSnapshotSession = false;
            BuildStepTree(_program);
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
        _breakpointNodeKeys.Clear();
        foreach (var key in SelectedScenarioPreset.BreakpointKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _breakpointNodeKeys.Add(key);
            }
        }
        SelectedFilePath = ResolveProgramFileForPreset(SelectedScenarioPreset);
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            ResolveProgramFromCurrentFolder(false);
        }
        else
        {
            UpdateConfigurationSummary();
        }

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
            SelectedProgramFileName = string.IsNullOrWhiteSpace(SelectedFilePath) ? null : Path.GetFileName(SelectedFilePath),
            SelectedProgramFileChecksumSha256 = ComputeFileSha256OrNull(SelectedFilePath),
            WiringFolderPath = WiringFolderPath,
            SimulationModelFolderPath = SimulationModelFolderPath,
            PythonScriptPath = PythonScriptPath,
            BreakpointKeys = _breakpointNodeKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList()
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

    private void OnUpgradeScenarioPreset(object sender, RoutedEventArgs e)
    {
        if (SelectedScenarioPreset == null)
        {
            AddLog("Kein Szenario zum Upgrade ausgewaehlt.");
            return;
        }

        ResolveProgramFromCurrentFolder(false);
        if (string.IsNullOrWhiteSpace(SelectedFilePath) && !string.IsNullOrWhiteSpace(SelectedScenarioPreset.TestProgramFolderPath))
        {
            SelectedFilePath = ResolveProgramFileForPreset(SelectedScenarioPreset);
        }

        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !LoadProgramFile(SelectedFilePath, true))
        {
            AddLog("Szenario-Upgrade abgebrochen: Programm konnte nicht geladen werden.");
            return;
        }

        var availableTargets = BuildBreakpointUpgradeTargets();
        var availableKeys = availableTargets.Select(item => item.NodeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolvedKeys = SelectedScenarioPreset.BreakpointKeys
            .Where(key => availableKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unresolvedKeys = SelectedScenarioPreset.BreakpointKeys
            .Where(key => !availableKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var upgradedKeys = new HashSet<string>(resolvedKeys, StringComparer.OrdinalIgnoreCase);
        if (unresolvedKeys.Count > 0)
        {
            var items = unresolvedKeys
                .Select(key => new BreakpointUpgradeItem(key, availableTargets))
                .ToList();
            var dialog = new ScenarioPresetUpgradeWindow(this, SelectedScenarioPreset.Name, items);
            if (dialog.ShowDialog() != true)
            {
                AddLog("Szenario-Upgrade abgebrochen.");
                return;
            }

            foreach (var mapping in dialog.SelectedMappings)
            {
                upgradedKeys.Add(mapping.Value);
            }
        }

        SelectedScenarioPreset.SelectedProgramFileName = string.IsNullOrWhiteSpace(SelectedFilePath) ? null : Path.GetFileName(SelectedFilePath);
        SelectedScenarioPreset.SelectedProgramFileChecksumSha256 = ComputeFileSha256OrNull(SelectedFilePath);
        SelectedScenarioPreset.BreakpointKeys = upgradedKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

        _breakpointNodeKeys.Clear();
        foreach (var key in SelectedScenarioPreset.BreakpointKeys)
        {
            _breakpointNodeKeys.Add(key);
        }

        BuildStepTree(_program!);
        _scenarioPresetStore.Save(ScenarioPresets);
        AddLog(unresolvedKeys.Count == 0
            ? $"Szenario-Upgrade abgeschlossen: {SelectedScenarioPreset.Name}"
            : $"Szenario-Upgrade abgeschlossen: {SelectedScenarioPreset.Name} ({upgradedKeys.Count}/{SelectedScenarioPreset.BreakpointKeys.Count} Breakpoints uebernommen)");
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

    private string? ResolveProgramFileForPreset(ScenarioPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.TestProgramFolderPath) || !Directory.Exists(preset.TestProgramFolderPath))
        {
            return null;
        }

        var programs = Directory.GetFiles(preset.TestProgramFolderPath, "*.ctxprg", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (programs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preset.SelectedProgramFileChecksumSha256))
        {
            foreach (var candidate in programs)
            {
                var checksum = ComputeFileSha256OrNull(candidate);
                if (string.Equals(checksum, preset.SelectedProgramFileChecksumSha256, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"Programm per Checksumme aufgeloest: {Path.GetFileName(candidate)}");
                    return candidate;
                }
            }

            AddLog("Gespeicherte Programm-Checksumme wurde im aktuellen Ordner nicht gefunden. Fallback auf Dateiname/Standardauswahl.");
        }

        if (!string.IsNullOrWhiteSpace(preset.SelectedProgramFileName))
        {
            var nameMatch = programs.FirstOrDefault(path => string.Equals(Path.GetFileName(path), preset.SelectedProgramFileName, StringComparison.OrdinalIgnoreCase));
            if (nameMatch != null)
            {
                AddLog($"Programm per Dateiname aufgeloest: {Path.GetFileName(nameMatch)}");
                return nameMatch;
            }

            AddLog("Gespeicherter Programm-Dateiname wurde im aktuellen Ordner nicht gefunden. Fallback auf Standardauswahl.");
        }

        return programs[0];
    }

    private static string? ComputeFileSha256OrNull(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private IReadOnlyList<BreakpointUpgradeTarget> BuildBreakpointUpgradeTargets()
    {
        var targets = new List<BreakpointUpgradeTarget>();
        foreach (var node in EnumerateBreakpointNodes(StepTreeRootNodes))
        {
            var displayName = $"{(node.IsGroup ? "Gruppe" : "Test")} | {BuildNodePath(node)}";
            targets.Add(new BreakpointUpgradeTarget(node.NodeKey, displayName));
        }

        return targets;
    }

    private static IEnumerable<StepTreeNodeViewModel> EnumerateBreakpointNodes(System.Collections.Generic.IEnumerable<StepTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsGroup || !string.IsNullOrWhiteSpace(node.NodeKey))
            {
                yield return node;
            }

            foreach (var child in EnumerateBreakpointNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static string BuildNodePath(StepTreeNodeViewModel node)
    {
        var parts = new System.Collections.Generic.Stack<string>();
        var current = node;
        while (current != null)
        {
            parts.Push(current.Title);
            current = current.Parent;
        }

        return string.Join(" > ", parts);
    }
}
