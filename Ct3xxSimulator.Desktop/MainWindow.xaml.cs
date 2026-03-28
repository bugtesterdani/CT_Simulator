// Provides Main Window for the desktop application support code.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Desktop.Configuration;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxSimulator.Simulation;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged, ISimulationObserver, IInteractionProvider, IMeasurementInteractionProvider, ISimulationExecutionController
{
    private readonly Ct3xxProgramFileParser _fileParser = new();
    private ScenarioPresetStore _scenarioPresetStore = new();
    private readonly ManualResetEventSlim _stepGate = new(initialState: true);
    private Ct3xxProgramFileSet? _fileSet;
    private Ct3xxProgram? _program;
    private CancellationTokenSource? _cts;
    private PythonDeviceProcessHost? _pythonDeviceHost;
    private LiveStateWindow? _liveStateWindow;
    private Views.EvaluationDetailsWindow? _evaluationDetailsWindow;
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
    private string? _csvReplayFilePath;
    private string? _scenarioPresetFilePath = ScenarioPresetStore.GetDefaultPath();
    private bool _isSimulationRunning;
    private string? _currentStep;
    private string? _configurationSummary;
    private string? _validationSummary;
    private string? _csvReplaySummary = "CSV-Modus: Aus";
    private ScenarioPreset? _selectedScenarioPreset;
    private CsvReplayMode _selectedCsvReplayMode;
    private ImportedTestRun? _loadedCsvReplayRun;
    private ImportedTestRunMatchReport? _csvReplayMatchReport;
    private string? _csvReplayError;
    private readonly List<ImportedTestRunStepMatch> _activeCsvReplayMatches = new();
    private int _csvReplayMatchCursor;
    private bool _isStepModeEnabled;
    private bool _pauseAtNextStep;
    private int _executedTestCount;
    private int? _replayPauseAfterStepCount;
    private bool _suppressCancellationLogOnce;
    private bool _isLoadedSnapshotSession;
    private SimulationTimelineEntry? _selectedTimelineEntry;
    private readonly Dictionary<Test, StepTreeNodeViewModel> _stepTreeNodes = new();
    private readonly Dictionary<Group, StepTreeNodeViewModel> _groupTreeNodes = new();
    private readonly Dictionary<StepTreeNodeViewModel, Test> _treeNodeTests = new();
    private readonly Dictionary<StepTreeNodeViewModel, Group> _treeNodeGroups = new();
    private readonly List<StepEvaluationHistoryEntry> _stepEvaluationHistory = new();
    private readonly HashSet<Test> _breakpointTests = new();
    private readonly HashSet<Group> _breakpointGroups = new();
    private readonly HashSet<string> _breakpointNodeKeys = new(StringComparer.OrdinalIgnoreCase);
    private StepTreeNodeViewModel? _selectedStepTreeNode;
    private string _simulationRunStateText = "Bereit";
    private Brush _simulationRunStateBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5A6470"));

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<StepResultViewModel> StepResults { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<StepTreeNodeViewModel> StepTreeRootNodes { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<ScenarioPreset> ScenarioPresets { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<SimulationTimelineEntry> TimelineEntries { get; } = new();

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

    public string? ScenarioPresetFilePath
    {
        get => _scenarioPresetFilePath;
        set
        {
            if (SetField(ref _scenarioPresetFilePath, value))
            {
                _scenarioPresetStore = new ScenarioPresetStore(value);
                UpdateConfigurationSummary();
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
                if (!value)
                {
                    SimulationRunStateText = "Bereit";
                    SimulationRunStateBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5A6470"));
                }

                OnPropertyChanged(nameof(CanStartSimulation));
            }
        }
    }

    public string? CsvReplayFilePath
    {
        get => _csvReplayFilePath;
        set
        {
            if (SetField(ref _csvReplayFilePath, value))
            {
                UpdateConfigurationSummary();
                RefreshCsvReplayState(false);
                ValidateCurrentConfiguration(false);
            }
        }
    }

    public IReadOnlyList<KeyValuePair<CsvReplayMode, string>> CsvReplayModeOptions { get; } =
        new[]
        {
            new KeyValuePair<CsvReplayMode, string>(CsvReplayMode.Off, "Aus"),
            new KeyValuePair<CsvReplayMode, string>(CsvReplayMode.Compare, "Vergleich"),
            new KeyValuePair<CsvReplayMode, string>(CsvReplayMode.CsvDrivesResult, "CSV fuehrt Ergebnis")
        };

    public CsvReplayMode SelectedCsvReplayMode
    {
        get => _selectedCsvReplayMode;
        set
        {
            if (SetField(ref _selectedCsvReplayMode, value))
            {
                UpdateConfigurationSummary();
                RefreshCsvReplayState(false);
                ValidateCurrentConfiguration(false);
            }
        }
    }

    public string SimulationRunStateText
    {
        get => _simulationRunStateText;
        private set => SetField(ref _simulationRunStateText, value);
    }

    public Brush SimulationRunStateBrush
    {
        get => _simulationRunStateBrush;
        private set => SetField(ref _simulationRunStateBrush, value);
    }

    /// <summary>
    /// Gets a value indicating whether the start simulation condition is met.
    /// </summary>
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

    public string? CsvReplaySummary
    {
        get => _csvReplaySummary;
        private set => SetField(ref _csvReplaySummary, value);
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

    /// <summary>
    /// Gets a value indicating whether the step backward condition is met.
    /// </summary>
    public bool CanStepBackward => _timelineIndex > 0;
    /// <summary>
    /// Gets a value indicating whether the step forward condition is met.
    /// </summary>
    public bool CanStepForward => _timelineIndex >= 0 && _timelineIndex < _timeline.Count - 1;

    public SimulationTimelineEntry? SelectedTimelineEntry
    {
        get => _selectedTimelineEntry;
        set
        {
            if (SetField(ref _selectedTimelineEntry, value) && value != null)
            {
                SelectTimelineIndex(value.Index, keepSelection: true);
            }
        }
    }

    public StepTreeNodeViewModel? SelectedStepTreeNode
    {
        get => _selectedStepTreeNode;
        set
        {
            if (SetField(ref _selectedStepTreeNode, value))
            {
                OnPropertyChanged(nameof(CanJumpToStepSnapshot));
                OnPropertyChanged(nameof(CanOpenEvaluationDetails));
                OnPropertyChanged(nameof(CanToggleBreakpoint));
                OnPropertyChanged(nameof(BreakpointButtonText));
            }
        }
    }

    /// <summary>
    /// Gets the latest timeline index for selected node.
    /// </summary>
    public bool CanJumpToStepSnapshot => GetLatestTimelineIndexForSelectedNode().HasValue;
    /// <summary>
    /// Gets a value indicating whether the open evaluation details condition is met.
    /// </summary>
    public bool CanOpenEvaluationDetails => SelectedStepTreeNode?.Result != null;
    /// <summary>
    /// Attempts to get selected node group.
    /// </summary>
    public bool CanToggleBreakpoint => SelectedStepTreeNode != null && (TryGetSelectedNodeTest(out _) || TryGetSelectedNodeGroup(out _));
    public string BreakpointButtonText
    {
        get
        {
            if (SelectedStepTreeNode == null)
            {
                return "Breakpoint setzen";
            }

            if (TryGetSelectedNodeTest(out var test))
            {
                return _breakpointTests.Contains(test) ? "Breakpoint entfernen" : "Breakpoint setzen";
            }

            if (TryGetSelectedNodeGroup(out var group))
            {
                return _breakpointGroups.Contains(group) ? "Breakpoint entfernen" : "Breakpoint setzen";
            }

            return "Breakpoint setzen";
        }
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

    private sealed class StepEvaluationHistoryEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StepEvaluationHistoryEntry"/> class.
        /// </summary>
        public StepEvaluationHistoryEntry(Test? test, StepResultViewModel result)
        {
            Test = test;
            Result = result;
        }

        /// <summary>
        /// Gets the test.
        /// </summary>
        public Test? Test { get; }
        /// <summary>
        /// Gets the result.
        /// </summary>
        public StepResultViewModel Result { get; }
    }
}
