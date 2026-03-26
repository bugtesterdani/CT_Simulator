using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
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

    public void OnProgramStarted(Ct3xxProgram program) => AddLog($"Programm gestartet: {program.ProgramVersion ?? program.Id ?? "unbekannt"}");
    public void OnLoopIteration(int iteration, int totalIterations) => AddLog($"DUT-Durchlauf {iteration}/{totalIterations}");
    public void OnGroupStarted(Group group)
    {
        Dispatcher.Invoke(() => SetGroupExpanded(group, true));
        AddLog($"Gruppe: {group.Name}");
    }
    public void OnGroupSkipped(Group group, string reason) => AddLog($"Gruppe uebersprungen: {group.Name} ({reason})");
    public void OnGroupCompleted(Group group)
    {
        Dispatcher.Invoke(() => SetGroupExpanded(group, false));
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
            var result = new StepResultViewModel(
                evaluation.StepName,
                evaluation.Outcome.ToString().ToUpperInvariant(),
                FormatNumber(evaluation.MeasuredValue),
                FormatNumber(evaluation.LowerLimit),
                FormatNumber(evaluation.UpperLimit),
                evaluation.Unit ?? string.Empty,
                evaluation.Details ?? string.Empty,
                evaluation.Traces,
                evaluation.CurvePoints,
                _timeline.Count == 0 ? null : _timeline.Count - 1);
            StepResults.Add(result);
            _stepEvaluationHistory.Add(new StepEvaluationHistoryEntry(test, result));
            ApplyEvaluationToStepTree(test, result);
        });
    }

    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoadedSnapshotSession = false;
            _latestStateSnapshot = snapshot;
            AppendTimelineSnapshot(snapshot);
            UpdateLiveStateWindow();
        });
    }

    private void OnStepTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SelectedStepTreeNode = e.NewValue as StepTreeNodeViewModel;
    }

    private void OnStepTreeDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SelectedStepTreeNode is not StepTreeNodeViewModel node || node.Result == null)
        {
            return;
        }

        var stepResult = node.Result;
        if (stepResult.Traces.Count == 0)
        {
            AddLog($"Keine Verbindungsansicht fuer '{stepResult.StepName}' verfuegbar.");
            return;
        }

        var window = new ConnectionGraphWindow(stepResult.StepName, stepResult.Traces, stepResult.CurvePoints) { Owner = this };
        window.ShowDialog();
    }

    public void OnMessage(string message) => AddLog(message);

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

    public string PromptMeasurement(Test test, Record record, string prompt, string? unit) => PromptInput(prompt);

    public bool PromptPassFail(string message)
    {
        return Dispatcher.Invoke(() => MessageBox.Show(this, message, "Operator", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
    }

    public void ShowMessage(string message, bool requiresConfirmation)
    {
        Dispatcher.Invoke(() => MessageBox.Show(this, message, "CT3xx", MessageBoxButton.OK, MessageBoxImage.Information));
    }

    private void OnShowLiveState(object sender, RoutedEventArgs e)
    {
        if (_liveStateWindow == null)
        {
            _liveStateWindow = new LiveStateWindow { Owner = this };
            _liveStateWindow.Closed += (_, _) => _liveStateWindow = null;
        }

        _liveStateWindow.Show();
        _liveStateWindow.Activate();
        UpdateLiveStateWindow();
    }

    private void UpdateLiveStateWindow()
    {
        if (_liveStateWindow == null || _timelineIndex < 0 || _timelineIndex >= _timeline.Count)
        {
            return;
        }

        _liveStateWindow.UpdateSnapshot(_timeline[_timelineIndex].Snapshot, BuildSignalHistoryUpToTimelineIndex(_timelineIndex));
    }

    private void OnExportResults(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Ergebnisse exportieren",
            Filter = "PDF (*.pdf)|*.pdf|JSON (*.json)|*.json|CSV (*.csv)|*.csv",
            FileName = $"ct3xx-results-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SimulationResultExportWriter.Write(dialog.FileName, CreateExportDocument());
        AddLog($"Ergebnisse exportiert: {Path.GetFileName(dialog.FileName)}");
    }

    private void OnSaveSnapshotSession(object sender, RoutedEventArgs e)
    {
        if (IsSimulationRunning)
        {
            MessageBox.Show(this, "Snapshot-Sessions koennen nur ausserhalb einer laufenden Simulation gespeichert werden.", "Snapshots speichern", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_timeline.Count == 0)
        {
            MessageBox.Show(this, "Es sind keine Snapshot-Daten vorhanden.", "Snapshots speichern", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Snapshot-Session speichern",
            Filter = "Snapshot Session (*.snapshot.json)|*.snapshot.json|JSON (*.json)|*.json",
            FileName = $"ct3xx-snapshots-{DateTime.Now:yyyyMMdd-HHmmss}.snapshot.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var exportDocument = CreateExportDocument();
        var document = SimulationSnapshotSessionSerializer.Create(
            DateTimeOffset.Now,
            ConfigurationSummary,
            exportDocument.Steps,
            exportDocument.Logs,
            _timeline.Select(item => item.Snapshot).ToList(),
            _signalHistory.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<MeasurementCurvePoint>)item.Value.ToList(),
                StringComparer.OrdinalIgnoreCase),
            _timelineIndex);

        SimulationSnapshotSessionSerializer.Save(dialog.FileName, document);
        AddLog($"Snapshot-Session gespeichert: {Path.GetFileName(dialog.FileName)}");
    }

    private void OnLoadSnapshotSession(object sender, RoutedEventArgs e)
    {
        if (IsSimulationRunning)
        {
            MessageBox.Show(this, "Snapshot-Sessions koennen nicht waehrend einer laufenden Simulation geladen werden.", "Snapshots laden", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Snapshot-Session laden",
            Filter = "Snapshot Session (*.snapshot.json)|*.snapshot.json|JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var document = SimulationSnapshotSessionSerializer.Load(dialog.FileName);
            ApplySnapshotSessionDocument(document);
            AddLog($"Snapshot-Session geladen: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Snapshot-Session konnte nicht geladen werden", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private SimulationExportDocument CreateExportDocument()
    {
        var steps = StepResults
            .Select(step => new SimulationExportStep(
                step.StepName,
                step.Outcome,
                step.MeasuredValue,
                step.LowerLimit,
                step.UpperLimit,
                step.Unit,
                step.Details,
                step.Traces,
                step.CurvePoints,
                step.TimelineIndex))
            .ToList();

        var logs = Logs
            .Select(log => new SimulationExportLogEntry(log.Timestamp, log.Message))
            .ToList();

        return new SimulationExportDocument(DateTimeOffset.Now, ConfigurationSummary, steps, logs);
    }

    private void ApplySnapshotSessionDocument(SimulationSnapshotSessionDocument document)
    {
        StepResults.Clear();
        StepTreeRootNodes.Clear();
        _stepEvaluationHistory.Clear();
        foreach (var step in document.Steps)
        {
            var result = new StepResultViewModel(
                step.StepName,
                step.Outcome,
                step.MeasuredValue,
                step.LowerLimit,
                step.UpperLimit,
                step.Unit,
                step.Details,
                step.Traces,
                step.CurvePoints,
                step.TimelineIndex);
            StepResults.Add(result);
            _stepEvaluationHistory.Add(new StepEvaluationHistoryEntry(null, result));
        }

        Logs.Clear();
        foreach (var log in document.Logs)
        {
            Logs.Add(new LogEntryViewModel(log.Message, log.Timestamp));
        }

        _timeline.Clear();
        TimelineEntries.Clear();
        foreach (var entry in document.Timeline.OrderBy(item => item.Index))
        {
            var snapshot = SimulationSnapshotSessionSerializer.ToSnapshot(entry);
            var timelineEntry = new SimulationTimelineEntry(entry.Index, snapshot);
            _timeline.Add(timelineEntry);
            TimelineEntries.Add(timelineEntry);
        }

        _signalHistory.Clear();
        foreach (var item in document.SignalHistory)
        {
            _signalHistory[item.Key] = item.Value
                .Select(point => new MeasurementCurvePoint(point.TimeMs, point.Label, point.Value, point.Unit))
                .ToList();
        }

        ConfigurationSummary = document.ConfigurationSummary;
        ValidationSummary = "Snapshot-Session geladen.";
        _isLoadedSnapshotSession = true;
        _latestStateSnapshot = _timeline.Count == 0 ? null : _timeline[Math.Clamp(document.SelectedTimelineIndex, 0, _timeline.Count - 1)].Snapshot;
        _timelineIndex = _timeline.Count == 0 ? -1 : Math.Clamp(document.SelectedTimelineIndex, 0, _timeline.Count - 1);
        if (_timelineIndex >= 0)
        {
            SelectTimelineIndex(_timelineIndex);
        }
        else
        {
            CurrentStep = null;
            RaiseTimelineNavigationChanged();
            UpdateLiveStateWindow();
        }
    }
}
