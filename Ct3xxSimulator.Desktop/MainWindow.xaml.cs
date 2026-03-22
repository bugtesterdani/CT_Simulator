using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Globalization;
using Microsoft.Win32;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Discovery;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Simulation;
using System.Windows.Threading;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged, ISimulationObserver, IInteractionProvider, IMeasurementInteractionProvider
{
    private readonly Ct3xxProgramFileParser _fileParser = new();
    private readonly Dictionary<object, SimulationNodeViewModel> _nodeLookup = new();
    private readonly DispatcherTimer _displayMessageTimer;
    private readonly ManualResetEventSlim _displayAcknowledgement = new(true);
    private readonly ObservableCollection<string> _measurementQueue = new();
    private Ct3xxProgramFileSet? _fileSet;
    private Ct3xxProgram? _program;
    private SimulationNodeViewModel? _selectedNode;
    private string? _selectedFilePath;
    private int _loopCount = 1;
    private bool _isSimulationRunning;
    private CancellationTokenSource? _cts;
    private string? _programDirectory;
    private string? _displayOverlayMessage;
    private string? _newMeasurementValue;
    private bool _isDisplayConfirmationRequired;
    private string? _sampleProgramSummary;
    private SampleProgramViewModel? _selectedSampleProgram;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _displayMessageTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _displayMessageTimer.Tick += (_, _) => ClearDisplayOverlay();
        Loaded += OnLoaded;
        Closed += (_, _) => _displayAcknowledgement.Dispose();
    }

    public ObservableCollection<SimulationNodeViewModel> Nodes { get; } = new();
    public ObservableCollection<LogEntryViewModel> Logs { get; } = new();
    public ObservableCollection<SampleProgramViewModel> AvailablePrograms { get; } = new();

    public SimulationNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => SetField(ref _selectedNode, value);
    }

    public SampleProgramViewModel? SelectedSampleProgram
    {
        get => _selectedSampleProgram;
        set
        {
            if (SetField(ref _selectedSampleProgram, value))
            {
                OnPropertyChanged(nameof(CanLoadSelectedSampleProgram));
                if (value != null)
                {
                    SelectedFilePath = value.FilePath;
                }
            }
        }
    }

    private void LoadSamplePrograms()
    {
        AvailablePrograms.Clear();
        var root = TestProgramDiscovery.FindRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            SampleProgramSummary = "Ordner 'testprogramme' wurde nicht gefunden.";
            return;
        }

        var entries = TestProgramDiscovery.EnumeratePrograms(root);
        foreach (var info in entries)
        {
            AvailablePrograms.Add(new SampleProgramViewModel(info));
        }

        SampleProgramSummary = entries.Count == 0
            ? $"Keine CTX-Programme unter '{root}' gefunden."
            : $"{entries.Count} Programme aus '{root}' verfügbar.";
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

    public int LoopCount
    {
        get => _loopCount;
        set
        {
            var normalized = Math.Max(1, value);
            SetField(ref _loopCount, normalized);
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

    public bool CanStartSimulation => !IsSimulationRunning && _program != null;
    public string? DisplayOverlayMessage
    {
        get => _displayOverlayMessage;
        private set
        {
            if (SetField(ref _displayOverlayMessage, value))
            {
                OnPropertyChanged(nameof(IsDisplayOverlayVisible));
            }
        }
    }

    public bool IsDisplayOverlayVisible => !string.IsNullOrWhiteSpace(DisplayOverlayMessage);
    public bool IsDisplayConfirmationRequired
    {
        get => _isDisplayConfirmationRequired;
        private set => SetField(ref _isDisplayConfirmationRequired, value);
    }
    public bool CanLoadSelectedSampleProgram => SelectedSampleProgram != null;
    public string? SampleProgramSummary
    {
        get => _sampleProgramSummary;
        private set => SetField(ref _sampleProgramSummary, value);
    }
    public ObservableCollection<string> MeasurementQueue => _measurementQueue;

    public string? NewMeasurementValue
    {
        get => _newMeasurementValue;
        set => SetField(ref _newMeasurementValue, value);
    }

    private void OnOpenProgram(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CT3xx Programm (*.ctxprg)|*.ctxprg|Alle Dateien (*.*)|*.*",
            Title = "CT3xx Programm öffnen"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadProgramFile(dialog.FileName);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        LoadSamplePrograms();
    }

    private void OnLoadSelectedSampleProgram(object sender, RoutedEventArgs e)
    {
        if (SelectedSampleProgram == null)
        {
            MessageBox.Show(this, "Bitte wählen Sie ein Testprogramm aus der Liste aus.", "Keine Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LoadProgramFile(SelectedSampleProgram.FilePath);
    }

    private void OnSampleProgramDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedSampleProgram == null)
        {
            return;
        }

        LoadProgramFile(SelectedSampleProgram.FilePath);
    }

    private void OnLoadProgramFromPath(object sender, RoutedEventArgs e)
    {
        var path = SelectedFilePath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(this, "Bitte geben Sie einen Pfad zu einer CT3xx Programmdatei ein.", "Kein Pfad angegeben", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LoadProgramFile(path);
    }

    private bool LoadProgramFile(string filePath, bool showErrors = true)
    {
        try
        {
            _fileSet = null;
            _program = null;
            var resolvedPath = Path.GetFullPath(filePath);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Datei nicht gefunden: {resolvedPath}");
            }

            var fileSet = _fileParser.Load(resolvedPath);
            _fileSet = fileSet;
            _program = fileSet.Program;
            OnPropertyChanged(nameof(CanStartSimulation));
            SelectedFilePath = resolvedPath;
            _programDirectory = Path.GetDirectoryName(resolvedPath);
            BuildTree(fileSet.Program);
            Logs.Clear();
            AddLog($"Programm geladen: {Path.GetFileName(resolvedPath)}");
            AddLog($"Externe CT3xx-Dateien: {fileSet.ExternalFiles.Count}");
            return true;
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                MessageBox.Show(this, ex.Message, "Fehler beim Laden", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                AddLog($"Fehler beim automatischen Laden: {ex.Message}");
            }

            return false;
        }
    }

    private void StartOverlayTimer(TimeSpan interval)
    {
        _displayMessageTimer.Stop();
        _displayMessageTimer.Interval = interval;
        _displayMessageTimer.Start();
    }

    private void OnAcknowledgeDisplayOverlay(object sender, RoutedEventArgs e)
    {
        CompleteDisplayAcknowledgement();
    }

    private void CompleteDisplayAcknowledgement()
    {
        IsDisplayConfirmationRequired = false;
        StartOverlayTimer(TimeSpan.FromSeconds(3));
        _displayAcknowledgement.Set();
    }

    private void ClearDisplayOverlay()
    {
        _displayMessageTimer.Stop();
        DisplayOverlayMessage = null;
        IsDisplayConfirmationRequired = false;
    }

    private async void OnStartSimulation(object sender, RoutedEventArgs e) => await StartSimulationAsync();

    private void OnCancelSimulation(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void OnAddMeasurement(object sender, RoutedEventArgs e)
    {
        if (TryProcessMeasurementInput(NewMeasurementValue))
        {
            NewMeasurementValue = string.Empty;
        }
    }

    private void OnAddFailMeasurement(object sender, RoutedEventArgs e)
    {
        TryProcessMeasurementInput("10");
    }

    private void OnAddPassMeasurement(object sender, RoutedEventArgs e)
    {
        TryProcessMeasurementInput("1000");
    }

    private void OnClearMeasurements(object sender, RoutedEventArgs e)
    {
        if (_measurementQueue.Count == 0)
        {
            return;
        }

        _measurementQueue.Clear();
        AddLog("Messwert-Warteschlange geleert.");
    }

    private void BuildTree(Ct3xxProgram program)
    {
        Nodes.Clear();
        _nodeLookup.Clear();
        var programTitle = TestDetailsFactory.Clean(program.ProgramVersion) ??
                           TestDetailsFactory.Clean(program.ProgramComment) ??
                           program.Id ??
                           "Programm";
        var root = new SimulationNodeViewModel($"Programm: {programTitle}", NodeType.Program, program)
        {
            Details = ProgramDetailsFactory.CreateProgramDetails(program)
        };
        Nodes.Add(root);

        AddTopLevelTables(root, program);

        var (startItems, endItems) = SplitProgramItems(program);

        if (startItems.Count > 0)
        {
            root.Children.Add(CreatePhaseNode("Test Start", startItems));
        }

        if (program.DutLoop != null)
        {
            var loopContainer = new SimulationNodeViewModel("Test Loop", NodeType.Section, program.DutLoop);
            loopContainer.Children.Add(CreateDutLoopNode(program.DutLoop));
            root.Children.Add(loopContainer);
        }

        if (endItems.Count > 0)
        {
            root.Children.Add(CreatePhaseNode("Test Ende", endItems));
        }

        if (program.DutLoop == null && startItems.Count == 0 && endItems.Count == 0)
        {
            root.Children.Add(new SimulationNodeViewModel("Keine Gruppen definiert", NodeType.Section, program));
        }

        SelectedNode = root;
    }

    private void AddTopLevelTables(SimulationNodeViewModel root, Ct3xxProgram program)
    {
        if (program.Tables.Count == 0)
        {
            return;
        }

        var title = $"Tabellen & Bibliotheken ({program.Tables.Count})";
        var container = new SimulationNodeViewModel(title, NodeType.Section, program.Tables);
        foreach (var table in program.Tables)
        {
            container.Children.Add(CreateTableNode(table));
        }

        root.Children.Add(container);
    }

    private (List<SequenceNode> StartItems, List<SequenceNode> EndItems) SplitProgramItems(Ct3xxProgram program)
    {
        var startItems = new List<SequenceNode>();
        var endItems = new List<SequenceNode>();
        var encounteredEnd = false;

        foreach (var item in program.RootItems)
        {
            if (!encounteredEnd &&
                item is Group marker &&
                string.Equals(marker.Id, "END$", StringComparison.OrdinalIgnoreCase))
            {
                encounteredEnd = true;
            }

            if (encounteredEnd)
            {
                endItems.Add(item);
            }
            else
            {
                startItems.Add(item);
            }
        }

        return (startItems, endItems);
    }

    private SimulationNodeViewModel CreatePhaseNode(string title, IEnumerable<SequenceNode> items)
    {
        var node = new SimulationNodeViewModel(title, NodeType.Section, items.ToList());
        foreach (var item in items)
        {
            switch (item)
            {
                case Group group:
                    node.Children.Add(CreateGroupNode(group));
                    break;
                case Test test:
                    node.Children.Add(CreateTestNode(test));
                    break;
                case Table table:
                    node.Children.Add(CreateTableNode(table));
                    break;
            }
        }

        return node;
    }

    private SimulationNodeViewModel CreateTableNode(Table table)
    {
        var title = TestDetailsFactory.Clean(table.Id) ?? TestDetailsFactory.Clean(table.File) ?? "Tabelle";
        var node = new SimulationNodeViewModel($"Tabelle: {title}", NodeType.Table, table)
        {
            Details = ProgramDetailsFactory.CreateTableDetails(table)
        };

        foreach (var library in table.Libraries)
        {
            node.Children.Add(CreateLibraryNode(library));
        }

        return node;
    }

    private SimulationNodeViewModel CreateLibraryNode(LibraryDefinition library)
    {
        var title = TestDetailsFactory.Clean(library.Name) ?? library.Id ?? "Bibliothek";
        var node = new SimulationNodeViewModel($"Bibliothek: {title}", NodeType.Library, library)
        {
            Details = ProgramDetailsFactory.CreateLibraryDetails(library)
        };

        foreach (var function in library.Functions)
        {
            node.Children.Add(CreateLibraryFunctionNode(function));
        }

        return node;
    }

    private SimulationNodeViewModel CreateLibraryFunctionNode(LibraryFunction function)
    {
        var title = TestDetailsFactory.Clean(function.Name) ?? function.Id ?? "Funktion";
        var node = new SimulationNodeViewModel($"Funktion: {title}", NodeType.Function, function)
        {
            Details = ProgramDetailsFactory.CreateLibraryFunctionDetails(function)
        };

        foreach (var table in function.Tables)
        {
            node.Children.Add(CreateTableNode(table));
        }

        foreach (var item in function.Items)
        {
            switch (item)
            {
                case Group group:
                    node.Children.Add(CreateGroupNode(group, isLibraryNode: true));
                    break;
                case Test test:
                    node.Children.Add(CreateTestNode(test, isLibraryNode: true));
                    break;
            }
        }

        return node;
    }

    private bool TryProcessMeasurementInput(string? rawValue)
    {
        if (!TryNormalizeMeasurementValue(rawValue, out var normalized, out var display))
        {
            MessageBox.Show(this, "Bitte einen numerischen Wert (z.B. 10 oder 1000) eingeben.", "Ungültiger Messwert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }


        _measurementQueue.Add(normalized);
        AddLog($"Messwert geplant: {display}");
        return true;
    }

    private static bool TryNormalizeMeasurementValue(string? rawValue, out string normalized, out string display)
    {
        normalized = string.Empty;
        display = string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var cleaned = rawValue.Trim();
        if (cleaned.EndsWith("V", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^1];
        }

        cleaned = cleaned.Replace(',', '.');
        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
        {
            return false;
        }

        normalized = numeric.ToString("0.###", CultureInfo.InvariantCulture);
        display = $"{normalized} V";
        return true;
    }

    private string? ConsumeQueuedMeasurementValue()
    {
        string? value = null;
        Dispatcher.Invoke(() =>
        {
            if (_measurementQueue.Count > 0)
            {
                value = _measurementQueue[0];
                _measurementQueue.RemoveAt(0);
            }
        });
        return value;
    }

    private static string BuildMeasurementLabel(Test test, Record record, string? unit)
    {
        var label = record.DrawingReference ?? record.Text ?? record.Id ?? test.Parameters?.Name ?? test.Name ?? "Messwert";
        label = string.IsNullOrWhiteSpace(label) ? "Messwert" : label.Trim();
        if (!string.IsNullOrWhiteSpace(unit))
        {
            label = $"{label} [{unit.Trim()}]";
        }

        return label;
    }

    private SimulationNodeViewModel CreateGroupNode(Group group, bool isLibraryNode = false)
    {
        var title = string.IsNullOrWhiteSpace(group.Name) ? $"Gruppe {group.Id}" : group.Name;
        var node = new SimulationNodeViewModel(title, NodeType.Group, group);
        if (!isLibraryNode)
        {
            _nodeLookup[group] = node;
        }
        node.Details = GroupDetailsFactory.Create(group);
        foreach (var item in group.Items)
        {
            switch (item)
            {
                case Group nested:
                    node.Children.Add(CreateGroupNode(nested, isLibraryNode));
                    break;
                case Test test:
                    node.Children.Add(CreateTestNode(test, isLibraryNode));
                    break;
            }
        }

        return node;
    }

    private SimulationNodeViewModel CreateDutLoopNode(DutLoop loop)
    {
        var title = string.IsNullOrWhiteSpace(loop.Name) ? "DUT Loop" : loop.Name;
        var node = new SimulationNodeViewModel(title, NodeType.DutLoop, loop);
        _nodeLookup[loop] = node;
        foreach (var item in loop.Items)
        {
            switch (item)
            {
                case Group nested:
                    node.Children.Add(CreateGroupNode(nested));
                    break;
                case Test test:
                    node.Children.Add(CreateTestNode(test));
                    break;
            }
        }

        return node;
    }

    private SimulationNodeViewModel CreateTestNode(Test test, bool isLibraryNode = false)
    {
        var preferredName = test.Parameters?.Name;
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            preferredName = string.IsNullOrWhiteSpace(test.Name) ? null : test.Name;
        }

        var title = string.IsNullOrWhiteSpace(preferredName) ? $"Test {test.Id}" : preferredName!;
        var node = new SimulationNodeViewModel(title, NodeType.Test, test);
        if (!isLibraryNode)
        {
            _nodeLookup[test] = node;
        }
        node.Details = TestDetailsFactory.Create(test, _programDirectory);
        return node;
    }

    private void ResetNodeStatuses()
    {
        foreach (var node in Nodes)
        {
            node.Reset();
        }
    }

    private void AddLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            Logs.Add(new LogEntryViewModel(message));
            if (Logs.Count > 500)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    private void OnSelectedNodeChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is SimulationNodeViewModel vm)
        {
            SelectedNode = vm;
        }
    }

    #region IInteractionProvider
    public string PromptSelection(string message, IReadOnlyList<string> options)
    {
        return Dispatcher.Invoke(() =>
        {
            if (options.Count == 0)
            {
                var dialog = new InputDialog(this, message);
                return dialog.ShowDialog() == true ? dialog.Response : string.Empty;
            }

            var selectionDialog = new SelectionDialog(this, message, options);
            return selectionDialog.ShowDialog() == true ? selectionDialog.SelectedOption : options.FirstOrDefault() ?? string.Empty;
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
        var label = BuildMeasurementLabel(test, record, unit);
        var unitDisplay = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit?.Trim()}";
        var queued = ConsumeQueuedMeasurementValue();
        if (!string.IsNullOrWhiteSpace(queued))
        {
            AddLog($"Messwert aus Warteschlange für {label}: {queued}{unitDisplay}");
            return queued!;
        }

        return Dispatcher.Invoke(() =>
        {
            var dialogPrompt = string.IsNullOrWhiteSpace(unit)
                ? $"{label}:"
                : $"{label} ({unit.Trim()}):";
            var dialog = new InputDialog(this, dialogPrompt);
            var result = dialog.ShowDialog() == true ? dialog.Response : string.Empty;
            var display = string.IsNullOrWhiteSpace(result) ? "n/a" : result;
            AddLog($"Messwert eingegeben: {label} = {display}{unitDisplay}");
            return result;
        });
    }

    public bool PromptPassFail(string message)
    {
        return Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(this, message, "Operator", MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        });
    }

    public void ShowMessage(string message, bool requiresConfirmation)
    {
        AddLog(message);
        if (!requiresConfirmation)
        {
            Dispatcher.Invoke(() =>
            {
                DisplayOverlayMessage = message;
                IsDisplayConfirmationRequired = false;
                StartOverlayTimer(TimeSpan.FromSeconds(4));
            });
            return;
        }

        _displayAcknowledgement.Reset();
        Dispatcher.Invoke(() =>
        {
            DisplayOverlayMessage = message;
            IsDisplayConfirmationRequired = true;
            _displayMessageTimer.Stop();
        });

        _displayAcknowledgement.Wait();
    }

    #endregion

    #region ISimulationObserver
    public void OnProgramStarted(Ct3xxProgram program)
    {
        AddLog($"Programm gestartet: {program.ProgramVersion ?? "Unbekannt"}");
        if (Nodes.FirstOrDefault() is { } root)
        {
            Dispatcher.Invoke(() => root.Status = NodeStatus.Running);
        }
    }

    public void OnLoopIteration(int iteration, int totalIterations)
    {
        AddLog($"== DUT Zyklus {iteration}/{totalIterations} ==");
        if (_program?.DutLoop != null && _nodeLookup.TryGetValue(_program.DutLoop, out var node))
        {
            Dispatcher.Invoke(() =>
            {
                node.Status = NodeStatus.Running;
                node.LastResult = $"Zyklus {iteration}/{totalIterations}";
                if (iteration == totalIterations)
                {
                    node.LastResult = "Abgeschlossen";
                }
            });
        }
    }

    public void OnGroupStarted(Group group)
    {
        AddLog($"-- Gruppe: {group.Name}");
        if (_nodeLookup.TryGetValue(group, out var node))
        {
            Dispatcher.Invoke(() => node.Status = NodeStatus.Running);
        }
    }

    public void OnGroupSkipped(Group group, string reason)
    {
        AddLog($"Gruppe übersprungen: {group.Name} ({reason})");
        if (_nodeLookup.TryGetValue(group, out var node))
        {
            Dispatcher.Invoke(() =>
            {
                node.Status = NodeStatus.Skipped;
                node.LastResult = reason;
            });
        }
    }

    public void OnGroupCompleted(Group group)
    {
        if (_nodeLookup.TryGetValue(group, out var node) && node.Status != NodeStatus.Skipped)
        {
            Dispatcher.Invoke(() => node.Status = NodeStatus.Completed);
        }
    }

    public void OnTestStarted(Test test)
    {
        var title = test.Parameters?.Name;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = test.Name;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = test.Id;
        }

        title ??= "Unnamed Test";
        AddLog($"   Test: {title}");
        if (_nodeLookup.TryGetValue(test, out var node))
        {
            Dispatcher.Invoke(() =>
            {
                node.Status = NodeStatus.Running;
                node.LastResult = null;
            });
        }
    }

    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        if (_nodeLookup.TryGetValue(test, out var node))
        {
            Dispatcher.Invoke(() =>
            {
                node.Status = outcome switch
                {
                    TestOutcome.Pass => NodeStatus.Completed,
                    TestOutcome.Fail => NodeStatus.Failed,
                    TestOutcome.Error => NodeStatus.Skipped,
                    _ => NodeStatus.Completed
                };
                node.LastResult = outcome.ToString().ToUpperInvariant();
            });
        }

        AddLog($"   Ergebnis: {outcome.ToString().ToUpperInvariant()}");
    }

    public void OnMessage(string message)
    {
        AddLog(message);
    }
    #endregion

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

    private async Task StartSimulationAsync()
    {
        AddLog("Simulation manuell gestartet.");
        if (_program == null)
        {
            MessageBox.Show(this, "Bitte laden Sie zuerst ein CT3xx Programm.", "Kein Programm", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsSimulationRunning)
        {
            return;
        }

        ResetNodeStatuses();
        _cts = new CancellationTokenSource();
        IsSimulationRunning = true;

        try
        {
            await Task.Run(() =>
            {
                var simulator = new Ct3xxProgramSimulator(this, this);
                if (_fileSet != null)
                {
                    simulator.Run(_fileSet, LoopCount, _cts.Token);
                }
                else
                {
                    simulator.Run(_program, LoopCount, _cts.Token);
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
            Dispatcher.Invoke(() => MessageBox.Show(this, ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error));
            AddLog($"Fehler: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsSimulationRunning = false;
            Dispatcher.Invoke(() =>
            {
                if (Nodes.FirstOrDefault() is { } root)
                {
                    root.Status = NodeStatus.Completed;
                }
            });
        }
    }

}








