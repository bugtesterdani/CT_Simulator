using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged, ISimulationObserver, IInteractionProvider, IMeasurementInteractionProvider
{
    private readonly Ct3xxProgramFileParser _fileParser = new();
    private Ct3xxProgramFileSet? _fileSet;
    private Ct3xxProgram? _program;
    private CancellationTokenSource? _cts;
    private PythonDeviceProcessHost? _pythonDeviceHost;
    private string? _previousPythonPipe;
    private string? _previousWireVizRoot;
    private string? _previousSimulationModelRoot;
    private string? _selectedFilePath;
    private string? _loadedFilePath;
    private string? _testProgramFolderPath;
    private string? _wiringFolderPath;
    private string? _simulationModelFolderPath;
    private string? _pythonScriptPath;
    private bool _isSimulationRunning;
    private string? _currentStep;
    private string? _configurationSummary;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public ObservableCollection<StepResultViewModel> StepResults { get; } = new();
    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();

    public string? TestProgramFolderPath
    {
        get => _testProgramFolderPath;
        set
        {
            if (SetField(ref _testProgramFolderPath, value))
            {
                ResolveProgramFromCurrentFolder(promptIfMultiple: false);
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetField(ref _selectedFilePath, value))
            {
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public string? WiringFolderPath
    {
        get => _wiringFolderPath;
        set
        {
            if (SetField(ref _wiringFolderPath, value))
            {
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public string? SimulationModelFolderPath
    {
        get => _simulationModelFolderPath;
        set
        {
            if (SetField(ref _simulationModelFolderPath, value))
            {
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public string? PythonScriptPath
    {
        get => _pythonScriptPath;
        set
        {
            if (SetField(ref _pythonScriptPath, value))
            {
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public bool IsSimulationRunning
    {
        get => _isSimulationRunning;
        private set
        {
            if (SetField(ref _isSimulationRunning, value))
            {
                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public bool CanStartSimulation =>
        !IsSimulationRunning &&
        !string.IsNullOrWhiteSpace(SelectedFilePath) &&
        !string.IsNullOrWhiteSpace(WiringFolderPath) &&
        !string.IsNullOrWhiteSpace(SimulationModelFolderPath) &&
        !string.IsNullOrWhiteSpace(PythonScriptPath);

    public string? CurrentStep
    {
        get => _currentStep;
        private set => SetField(ref _currentStep, value);
    }

    public string? ConfigurationSummary
    {
        get => _configurationSummary;
        private set => SetField(ref _configurationSummary, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyDefaultScenario();
        UpdateConfigurationSummary();
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
        PythonScriptPath = Path.Combine(simtestRoot, "device", "device_39.py");

        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            LoadProgramFile(SelectedFilePath, showErrors: false);
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
        var dialog = new OpenFolderDialog
        {
            Title = "Ordner mit CT3xx-Testprogramm wählen"
        };

        if (dialog.ShowDialog() == true)
        {
            TestProgramFolderPath = dialog.FolderName;
            ResolveProgramFromCurrentFolder(promptIfMultiple: true);
            if (!string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                LoadProgramFile(SelectedFilePath, showErrors: true);
            }
        }
    }

    private void OnBrowseWiringFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Ordner mit WireViz-Verdrahtung wählen"
        };

        if (dialog.ShowDialog() == true)
        {
            WiringFolderPath = dialog.FolderName;
            UpdateConfigurationSummary();
        }
    }

    private void OnBrowseSimulationModelFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Ordner mit Simulationsmodell wählen"
        };

        if (dialog.ShowDialog() == true)
        {
            SimulationModelFolderPath = dialog.FolderName;
            UpdateConfigurationSummary();
        }
    }

    private void OnBrowsePythonScript(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Python-Geräteskript wählen",
            Filter = "Python (*.py)|*.py|Alle Dateien (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PythonScriptPath = dialog.FileName;
            UpdateConfigurationSummary();
        }
    }

    private void OnLoadProgramFromResolvedSelection(object sender, RoutedEventArgs e)
    {
        ResolveProgramFromCurrentFolder(promptIfMultiple: true);
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

        var choice = PromptSelection("Mehrere CT3xx-Programme gefunden. Bitte wählen:", programs);
        return string.IsNullOrWhiteSpace(choice) ? programs[0] : choice;
    }

    private bool LoadProgramFile(string filePath, bool showErrors = true)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            _fileSet = _fileParser.Load(fullPath);
            _program = _fileSet.Program;
            _loadedFilePath = fullPath;
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

    private async void OnStartSimulation(object sender, RoutedEventArgs e)
    {
        await StartSimulationAsync();
    }

    private void OnCancelSimulation(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private async Task StartSimulationAsync()
    {
        ResolveProgramFromCurrentFolder(promptIfMultiple: true);
        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !LoadProgramFile(SelectedFilePath, showErrors: true))
        {
            return;
        }

        if (!CanStartSimulation)
        {
            MessageBox.Show(this, "Bitte Testprogramm-Ordner, Verdrahtungs-Ordner, Simulations-Ordner und Python-Skript auswählen.", "Konfiguration unvollständig", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        StepResults.Clear();
        Logs.Clear();
        CurrentStep = null;
        IsSimulationRunning = true;
        _cts = new CancellationTokenSource();

        try
        {
            var loadedProgram = _program;
            if (loadedProgram == null)
            {
                return;
            }

            ApplySimulationOverrides();
            EnsurePythonDevice();

            await Task.Run(() =>
            {
                var simulator = new Ct3xxProgramSimulator(this, this);
                if (_fileSet != null)
                {
                    simulator.Run(_fileSet, 1, _cts.Token);
                }
                else
                {
                    simulator.Run(loadedProgram, 1, _cts.Token);
                }
            });

            AddLog("Simulation abgeschlossen.");
        }
        catch (OperationCanceledException)
        {
            AddLog("Simulation abgebrochen.");
        }
        catch (Exception ex)
        {
            AddLog($"Fehler: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DisposePythonDeviceHost();
            RestoreSimulationOverrides();
            _cts?.Dispose();
            _cts = null;
            IsSimulationRunning = false;
            CurrentStep = null;
        }
    }

    private void ApplySimulationOverrides()
    {
        _previousWireVizRoot = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", EnvironmentVariableTarget.Process);
        _previousSimulationModelRoot = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", EnvironmentVariableTarget.Process);

        Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", WiringFolderPath, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", SimulationModelFolderPath, EnvironmentVariableTarget.Process);

        AddLog($"WireViz-Ordner: {WiringFolderPath}");
        AddLog($"Simulations-Ordner: {SimulationModelFolderPath}");
    }

    private void RestoreSimulationOverrides()
    {
        Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", _previousWireVizRoot, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", _previousSimulationModelRoot, EnvironmentVariableTarget.Process);
        _previousWireVizRoot = null;
        _previousSimulationModelRoot = null;
    }

    private void EnsurePythonDevice()
    {
        DisposePythonDeviceHost();

        if (string.IsNullOrWhiteSpace(PythonScriptPath))
        {
            return;
        }

        _pythonDeviceHost = PythonDeviceProcessHost.Start(PythonScriptPath);
        if (_pythonDeviceHost == null)
        {
            throw new InvalidOperationException("Das gewählte Python-Skript konnte nicht gestartet werden.");
        }

        _previousPythonPipe = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", _pythonDeviceHost.PipePath, EnvironmentVariableTarget.Process);
        AddLog($"Python-Gerätesimulation gestartet: {Path.GetFileName(PythonScriptPath)}");
    }

    private void DisposePythonDeviceHost()
    {
        if (_pythonDeviceHost == null)
        {
            return;
        }

        _pythonDeviceHost.Dispose();
        _pythonDeviceHost = null;
        Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", _previousPythonPipe, EnvironmentVariableTarget.Process);
        _previousPythonPipe = null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DisposePythonDeviceHost();
        RestoreSimulationOverrides();
    }

    private void UpdateConfigurationSummary()
    {
        ConfigurationSummary =
            $"Programm: {Path.GetFileName(SelectedFilePath ?? string.Empty)} | " +
            $"Verdrahtung: {Path.GetFileName(WiringFolderPath ?? string.Empty)} | " +
            $"Simulation: {Path.GetFileName(SimulationModelFolderPath ?? string.Empty)} | " +
            $"Python: {Path.GetFileName(PythonScriptPath ?? string.Empty)}";
    }

    private void AddLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            Logs.Add(new LogEntryViewModel(message));
            if (Logs.Count > 200)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    public void OnProgramStarted(Ct3xxProgram program)
    {
        AddLog($"Programm gestartet: {program.ProgramVersion ?? program.Id ?? "unbekannt"}");
    }

    public void OnLoopIteration(int iteration, int totalIterations)
    {
        AddLog($"DUT-Durchlauf {iteration}/{totalIterations}");
    }

    public void OnGroupStarted(Group group)
    {
        AddLog($"Gruppe: {group.Name}");
    }

    public void OnGroupSkipped(Group group, string reason)
    {
        AddLog($"Gruppe übersprungen: {group.Name} ({reason})");
    }

    public void OnGroupCompleted(Group group)
    {
    }

    public void OnTestStarted(Test test)
    {
        CurrentStep = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        AddLog($"Starte: {CurrentStep}");
    }

    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        AddLog($"Ergebnis {CurrentStep}: {outcome.ToString().ToUpperInvariant()}");
    }

    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        Dispatcher.Invoke(() =>
        {
            StepResults.Add(new StepResultViewModel(
                evaluation.StepName,
                evaluation.Outcome.ToString().ToUpperInvariant(),
                FormatNumber(evaluation.MeasuredValue),
                FormatNumber(evaluation.LowerLimit),
                FormatNumber(evaluation.UpperLimit),
                evaluation.Unit ?? string.Empty,
                evaluation.Details ?? string.Empty));
        });
    }

    public void OnMessage(string message)
    {
        AddLog(message);
    }

    public string PromptSelection(string message, IReadOnlyList<string> options)
    {
        return Dispatcher.Invoke(() =>
        {
            var dialog = new SelectionDialog(this, message, options);
            return dialog.ShowDialog() == true ? dialog.SelectedOption : options.Count > 0 ? options[0] : string.Empty;
        });
    }

    public string PromptInput(string prompt)
    {
        return Dispatcher.Invoke(() =>
        {
            var dialog = new InputDialog(this, prompt);
            return dialog.ShowDialog() == true ? dialog.Response : string.Empty;
        });
    }

    public string PromptMeasurement(Test test, Record record, string prompt, string? unit)
    {
        return PromptInput(prompt);
    }

    public bool PromptPassFail(string message)
    {
        return Dispatcher.Invoke(() => MessageBox.Show(this, message, "Operator", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
    }

    public void ShowMessage(string message, bool requiresConfirmation)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, message, "CT3xx", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatNumber(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
    }
}
