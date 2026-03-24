using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Desktop.Configuration;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged, ISimulationObserver, IInteractionProvider, IMeasurementInteractionProvider, ISimulationExecutionController
{
    private readonly Ct3xxProgramFileParser _fileParser = new();
    private readonly ScenarioPresetStore _scenarioPresetStore = new();
    private readonly ManualResetEventSlim _stepGate = new(initialState: true);
    private Ct3xxProgramFileSet? _fileSet;
    private Ct3xxProgram? _program;
    private CancellationTokenSource? _cts;
    private PythonDeviceProcessHost? _pythonDeviceHost;
    private LiveStateWindow? _liveStateWindow;
    private SimulationStateSnapshot? _latestStateSnapshot;
    private readonly List<SimulationTimelineEntry> _timeline = new();
    private readonly Dictionary<string, List<MeasurementCurvePoint>> _signalHistory = new(StringComparer.OrdinalIgnoreCase);
    private int _timelineIndex = -1;
    private string? _previousPythonPipe;
    private string? _previousWireVizRoot;
    private string? _previousSimulationModelRoot;
    private string? _selectedFilePath;
    private string? _testProgramFolderPath;
    private string? _wiringFolderPath;
    private string? _simulationModelFolderPath;
    private string? _pythonScriptPath;
    private bool _isSimulationRunning;
    private string? _currentStep;
    private string? _configurationSummary;
    private string? _validationSummary;
    private ScenarioPreset? _selectedScenarioPreset;
    private bool _isStepModeEnabled;
    private bool _pauseAtNextStep;
    private int _executedTestCount;
    private int? _replayPauseAfterStepCount;
    private bool _suppressCancellationLogOnce;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public ObservableCollection<StepResultViewModel> StepResults { get; } = new();
    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();
    public ObservableCollection<ScenarioPreset> ScenarioPresets { get; } = new();

    public string? TestProgramFolderPath
    {
        get => _testProgramFolderPath;
        set
        {
            if (SetField(ref _testProgramFolderPath, value))
            {
                ResolveProgramFromCurrentFolder(promptIfMultiple: false);
                OnPropertyChanged(nameof(CanStartSimulation));
                ValidateCurrentConfiguration(false);
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
                ValidateCurrentConfiguration(false);
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
                UpdateConfigurationSummary();
                ValidateCurrentConfiguration(false);
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
                UpdateConfigurationSummary();
                ValidateCurrentConfiguration(false);
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
                UpdateConfigurationSummary();
                ValidateCurrentConfiguration(false);
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

    public string? ValidationSummary
    {
        get => _validationSummary;
        private set => SetField(ref _validationSummary, value);
    }

    public ScenarioPreset? SelectedScenarioPreset
    {
        get => _selectedScenarioPreset;
        set => SetField(ref _selectedScenarioPreset, value);
    }

    public bool IsStepModeEnabled
    {
        get => _isStepModeEnabled;
        set => SetField(ref _isStepModeEnabled, value);
    }

    public bool CanStepBackward => _timelineIndex > 0;
    public bool CanStepForward => _timelineIndex >= 0 && _timelineIndex < _timeline.Count - 1;

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
