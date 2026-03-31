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
using Ct3xxTestRunLogParser.Matching;
using Ct3xxTestRunLogParser.Parsing;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Executes OnLoaded.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _scenarioPresetStore = new ScenarioPresetStore(ScenarioPresetFilePath);
        LoadScenarioPresets();
        ApplyDefaultScenario();
        UpdateConfigurationSummary();
        ValidateCurrentConfiguration(false);
    }

    /// <summary>
    /// Executes ApplyDefaultScenario.
    /// </summary>
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
        SelectedCsvReplayMode = CsvReplayMode.Off;

        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath, false);
        }
    }

    /// <summary>
    /// Executes FindSimtestRoot.
    /// </summary>
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

    /// <summary>
    /// Executes OnBrowseProgramFolder.
    /// </summary>
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

    /// <summary>
    /// Executes OnBrowseWiringFolder.
    /// </summary>
    private void OnBrowseWiringFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit WireViz-Verdrahtung waehlen" };
        if (dialog.ShowDialog() == true)
        {
            WiringFolderPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// Executes OnBrowseSimulationModelFolder.
    /// </summary>
    private void OnBrowseSimulationModelFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit Simulationsmodell waehlen" };
        if (dialog.ShowDialog() == true)
        {
            SimulationModelFolderPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// Executes OnBrowsePythonScript.
    /// </summary>
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

    /// <summary>
    /// Executes OnBrowseCsvReplayFile.
    /// </summary>
    private void OnBrowseCsvReplayFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "CSV-Testlauf waehlen",
            Filter = "CSV (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
            FileName = Path.GetFileName(CsvReplayFilePath ?? "Testlauf.csv"),
            InitialDirectory = GetInitialDirectory(CsvReplayFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            CsvReplayFilePath = dialog.FileName;
        }
    }

    /// <summary>
    /// Executes OnLoadProgramFromResolvedSelection.
    /// </summary>
    private void OnLoadProgramFromResolvedSelection(object sender, RoutedEventArgs e)
    {
        ResolveProgramFromCurrentFolder(true);
        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath);
        }
    }

    /// <summary>
    /// Executes ResolveProgramFromCurrentFolder.
    /// </summary>
    private void ResolveProgramFromCurrentFolder(bool promptIfMultiple)
    {
        SelectedFilePath = ResolveProgramFileFromFolder(TestProgramFolderPath, promptIfMultiple);
        UpdateConfigurationSummary();
    }

    /// <summary>
    /// Executes ResolveProgramFileFromFolder.
    /// </summary>
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

    /// <summary>
    /// Executes LoadProgramFile.
    /// </summary>
    private bool LoadProgramFile(string filePath, bool showErrors = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            _fileSet = _fileParser.Load(fullPath);
            _program = _fileSet.Program;
            _wireVizResolver = null;
            _wireVizResolverProgramPath = null;
            _wireVizResolverWireVizRoot = null;
            _wireVizResolverSimulationRoot = null;
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
            RefreshCsvReplayState(false);
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
            RefreshCsvReplayState(false);
            return false;
        }
    }

    /// <summary>
    /// Executes UpdateConfigurationSummary.
    /// </summary>
    private void UpdateConfigurationSummary()
    {
        ConfigurationSummary =
            $"Programm: {Path.GetFileName(SelectedFilePath ?? string.Empty)} | " +
            $"Verdrahtung: {Path.GetFileName(WiringFolderPath ?? string.Empty)} | " +
            $"Simulation: {Path.GetFileName(SimulationModelFolderPath ?? string.Empty)} | " +
            $"Geraetemodell: {Path.GetFileName(PythonScriptPath ?? string.Empty)} | " +
            $"CSV: {Path.GetFileName(CsvReplayFilePath ?? string.Empty)} ({GetCsvReplayModeLabel(SelectedCsvReplayMode)}) | " +
            $"Szenario-Datei: {Path.GetFileName(ScenarioPresetFilePath ?? string.Empty)}";
    }

    /// <summary>
    /// Executes LoadScenarioPresets.
    /// </summary>
    private void LoadScenarioPresets()
    {
        ScenarioPresets.Clear();
        foreach (var preset in _scenarioPresetStore.Load().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            ScenarioPresets.Add(preset);
        }

        AddLog($"Szenario-Datei geladen: {_scenarioPresetStore.FilePath}");
    }

    /// <summary>
    /// Executes OnBrowseScenarioPresetFile.
    /// </summary>
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

    /// <summary>
    /// Executes OnLoadScenarioPresetFile.
    /// </summary>
    private void OnLoadScenarioPresetFile(object sender, RoutedEventArgs e)
    {
        _scenarioPresetStore = new ScenarioPresetStore(ScenarioPresetFilePath);
        LoadScenarioPresets();
        AddLog($"Szenario-Datei explizit geladen: {_scenarioPresetStore.FilePath}");
    }

    /// <summary>
    /// Executes OnSaveScenarioPresetFileAs.
    /// </summary>
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

    /// <summary>
    /// Executes OnApplyScenarioPreset.
    /// </summary>
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
        CsvReplayFilePath = SelectedScenarioPreset.CsvReplayFilePath;
        SelectedCsvReplayMode = SelectedScenarioPreset.CsvReplayMode;
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

    /// <summary>
    /// Executes OnSaveScenarioPreset.
    /// </summary>
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

        var existingCards = SelectedScenarioPreset?.InstalledCards;
        var existingMapping = SelectedScenarioPreset?.TestTypeCards;
        var existingPatterns = SelectedScenarioPreset?.CardIndexPatterns;
        var existingRules = SelectedScenarioPreset?.TestTypeCardRules;

        var preset = new ScenarioPreset
        {
            Name = name.Trim(),
            TestProgramFolderPath = TestProgramFolderPath,
            SelectedProgramFileName = string.IsNullOrWhiteSpace(SelectedFilePath) ? null : Path.GetFileName(SelectedFilePath),
            SelectedProgramFileChecksumSha256 = ComputeFileSha256OrNull(SelectedFilePath),
            WiringFolderPath = WiringFolderPath,
            SimulationModelFolderPath = SimulationModelFolderPath,
            PythonScriptPath = PythonScriptPath,
            CsvReplayFilePath = CsvReplayFilePath,
            CsvReplayMode = SelectedCsvReplayMode,
            BreakpointKeys = _breakpointNodeKeys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            InstalledCards = existingCards != null
                ? new System.Collections.Generic.Dictionary<string, int>(existingCards, StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.Dictionary<string, int>(),
            TestTypeCards = existingMapping != null
                ? new System.Collections.Generic.Dictionary<string, string>(existingMapping, StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.Dictionary<string, string>(),
            CardIndexPatterns = existingPatterns != null
                ? new System.Collections.Generic.Dictionary<string, string>(existingPatterns, StringComparer.OrdinalIgnoreCase)
                : new System.Collections.Generic.Dictionary<string, string>(),
            TestTypeCardRules = existingRules != null
                ? new System.Collections.Generic.List<Ct3xxSimulator.Validation.TestTypeCardRule>(existingRules.Select(rule => new Ct3xxSimulator.Validation.TestTypeCardRule
                {
                    TestType = rule.TestType,
                    Cards = rule.Cards != null ? new System.Collections.Generic.List<string>(rule.Cards) : new System.Collections.Generic.List<string>(),
                    MatchRegex = rule.MatchRegex
                }))
                : new System.Collections.Generic.List<Ct3xxSimulator.Validation.TestTypeCardRule>()
        };

        ScenarioPresets.Add(preset);
        SelectedScenarioPreset = preset;
        _scenarioPresetStore.Save(ScenarioPresets);
        AddLog($"Szenario gespeichert: {preset.Name}");
    }

    /// <summary>
    /// Executes OnDeleteScenarioPreset.
    /// </summary>
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

    /// <summary>
    /// Executes OnUpgradeScenarioPreset.
    /// </summary>
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

    /// <summary>
    /// Executes OnValidateConfiguration.
    /// </summary>
    private void OnValidateConfiguration(object sender, RoutedEventArgs e)
    {
        var issues = ValidateCurrentConfiguration(true);
        if (issues.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, issues), "Validierungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Executes ValidateCurrentConfiguration.
    /// </summary>
    private IReadOnlyList<string> ValidateCurrentConfiguration(bool showSuccessMessage)
    {
        var issues = SimulationConfigurationValidator.Validate(SelectedFilePath, WiringFolderPath, SimulationModelFolderPath, PythonScriptPath).ToList();
        issues.AddRange(ValidateCsvReplayConfiguration());
        issues.AddRange(ValidateTesterCardInventory());
        ValidationSummary = issues.Count == 0 ? "Validierung: OK" : $"Validierung: {issues.Count} Problem(e)";
        if (showSuccessMessage && issues.Count == 0)
        {
            MessageBox.Show(this, "Konfiguration ist gueltig.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        return issues;
    }

    /// <summary>
    /// Executes ValidateTesterCardInventory.
    /// </summary>
    private IReadOnlyList<string> ValidateTesterCardInventory()
    {
        if (_program == null || SelectedScenarioPreset == null)
        {
            return Array.Empty<string>();
        }

        var definition = new Ct3xxSimulator.Validation.CardInventoryDefinition
        {
            InstalledCards = SelectedScenarioPreset.InstalledCards ?? new System.Collections.Generic.Dictionary<string, int>(),
            TestTypeCards = SelectedScenarioPreset.TestTypeCards ?? new System.Collections.Generic.Dictionary<string, string>(),
            TestTypeCardRules = SelectedScenarioPreset.TestTypeCardRules ?? new System.Collections.Generic.List<Ct3xxSimulator.Validation.TestTypeCardRule>(),
            CardIndexPatterns = SelectedScenarioPreset.CardIndexPatterns ?? new System.Collections.Generic.Dictionary<string, string>()
        };

        if (!Ct3xxSimulator.Validation.CardInventoryValidator.HasCardConfiguration(definition))
        {
            return Array.Empty<string>();
        }

        return Ct3xxSimulator.Validation.CardInventoryValidator.Validate(_program, definition);
    }

    /// <summary>
    /// Executes GetInitialDirectory.
    /// </summary>
    private static string? GetInitialDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }

    /// <summary>
    /// Executes ResolveProgramFileForPreset.
    /// </summary>
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

    /// <summary>
    /// Executes ComputeFileSha256OrNull.
    /// </summary>
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

    /// <summary>
    /// Executes RefreshCsvReplayState.
    /// </summary>
    private void RefreshCsvReplayState(bool showErrors)
    {
        _loadedCsvReplayRun = null;
        _csvReplayMatchReport = null;
        _csvReplayError = null;
        _activeCsvReplayMatches.Clear();
        _csvReplayMatchCursor = 0;

        if (SelectedCsvReplayMode == CsvReplayMode.Off)
        {
            CsvReplaySummary = "CSV-Modus: Aus";
            return;
        }

        if (string.IsNullOrWhiteSpace(CsvReplayFilePath))
        {
            CsvReplaySummary = $"CSV-Modus: {GetCsvReplayModeLabel(SelectedCsvReplayMode)} | Keine Datei ausgewaehlt";
            return;
        }

        try
        {
            var parser = new TestRunLogCsvParser();
            _loadedCsvReplayRun = parser.ParseFile(CsvReplayFilePath);

            if (_program == null)
            {
                CsvReplaySummary = $"CSV geladen: {_loadedCsvReplayRun.Steps.Count} Zeilen | Programm-Matching ausstehend";
                return;
            }

            var matcher = new ImportedTestRunMatcher();
            _csvReplayMatchReport = matcher.Match(_program, _loadedCsvReplayRun);
            CsvReplaySummary =
                $"CSV geladen: {_loadedCsvReplayRun.Steps.Count} Zeilen | " +
                $"Modus: {GetCsvReplayModeLabel(SelectedCsvReplayMode)} | " +
                $"Matching: {_csvReplayMatchReport.Summary}{BuildCsvReplayReliabilitySuffix(_csvReplayMatchReport)}";
        }
        catch (Exception ex)
        {
            _csvReplayError = ex.Message;
            CsvReplaySummary = $"CSV-Fehler: {ex.Message}";
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "CSV-Testlauf konnte nicht geladen werden", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Executes ValidateCsvReplayConfiguration.
    /// </summary>
    private IReadOnlyList<string> ValidateCsvReplayConfiguration()
    {
        var issues = new System.Collections.Generic.List<string>();
        if (SelectedCsvReplayMode == CsvReplayMode.Off)
        {
            return issues;
        }

        if (string.IsNullOrWhiteSpace(CsvReplayFilePath) || !File.Exists(CsvReplayFilePath))
        {
            issues.Add("Der CSV-Testlauf wurde nicht gefunden.");
            return issues;
        }

        if (!string.Equals(Path.GetExtension(CsvReplayFilePath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("Der CSV-Testlauf muss als .csv-Datei vorliegen.");
        }

        if (!string.IsNullOrWhiteSpace(_csvReplayError))
        {
            issues.Add($"Der CSV-Testlauf konnte nicht gelesen werden: {_csvReplayError}");
            return issues;
        }

        if (_program != null && _csvReplayMatchReport != null && _csvReplayMatchReport.Matches.Count == 0)
        {
            issues.Add("Der CSV-Testlauf konnte keinem sichtbaren Programmschritt zugeordnet werden.");
            return issues;
        }

        if (_program != null && _csvReplayMatchReport != null && SelectedCsvReplayMode == CsvReplayMode.CsvDrivesResult && !_csvReplayMatchReport.IsReliable)
        {
            issues.Add("CSV fuehrt Ergebnis ist aktuell nicht zulaessig, weil das Matching zwischen Programm und CSV nicht zuverlaessig genug ist.");
        }

        return issues;
    }

    /// <summary>
    /// Executes GetCsvReplayModeLabel.
    /// </summary>
    private static string GetCsvReplayModeLabel(CsvReplayMode mode)
    {
        return mode switch
        {
            CsvReplayMode.Compare => "Vergleich",
            CsvReplayMode.CsvDrivesResult => "CSV fuehrt Ergebnis",
            _ => "Aus"
        };
    }

    /// <summary>
    /// Executes PrepareCsvReplayExecutionState.
    /// </summary>
    private void PrepareCsvReplayExecutionState()
    {
        _activeCsvReplayMatches.Clear();
        _csvReplayMatchCursor = 0;

        if (SelectedCsvReplayMode == CsvReplayMode.Off || _csvReplayMatchReport == null)
        {
            return;
        }

        foreach (var match in _csvReplayMatchReport.Matches.OrderBy(item => item.ProgramStep.SequenceIndex))
        {
            _activeCsvReplayMatches.Add(match);
        }

        AddLog($"CSV-Replay vorbereitet: {_activeCsvReplayMatches.Count} gematchte Schritte im Modus {GetCsvReplayModeLabel(SelectedCsvReplayMode)}.");
        if (!_csvReplayMatchReport.IsReliable)
        {
            AddLog($"Warnung: CSV-Matching ist nicht voll zuverlaessig. {_csvReplayMatchReport.Summary}");
        }
    }

    /// <summary>
    /// Executes BuildCsvReplayReliabilitySuffix.
    /// </summary>
    private static string BuildCsvReplayReliabilitySuffix(Ct3xxTestRunLogParser.Model.ImportedTestRunMatchReport report)
    {
        if (report.IsReliable)
        {
            return string.Empty;
        }

        return $" | Warnung: Unzuverlaessiges Matching ({report.Matches.Count} Treffer, {report.UnmatchedProgramSteps.Count} Programmschritte offen, {report.UnmatchedCsvSteps.Count} CSV-Zeilen offen)";
    }

    /// <summary>
    /// Executes BuildBreakpointUpgradeTargets.
    /// </summary>
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

    /// <summary>
    /// Executes EnumerateBreakpointNodes.
    /// </summary>
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

    /// <summary>
    /// Executes BuildNodePath.
    /// </summary>
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
