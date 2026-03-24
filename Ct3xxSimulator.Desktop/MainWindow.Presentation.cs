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
    public void OnGroupStarted(Group group) => AddLog($"Gruppe: {group.Name}");
    public void OnGroupSkipped(Group group, string reason) => AddLog($"Gruppe uebersprungen: {group.Name} ({reason})");
    public void OnGroupCompleted(Group group) { }

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
                evaluation.Details ?? string.Empty,
                evaluation.Traces,
                evaluation.CurvePoints));
        });
    }

    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            _latestStateSnapshot = snapshot;
            AppendTimelineSnapshot(snapshot);
            UpdateLiveStateWindow();
        });
    }

    private void OnStepResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: StepResultViewModel stepResult } dataGrid)
        {
            return;
        }

        if (stepResult.Traces.Count == 0)
        {
            AddLog($"Keine Verbindungsansicht fuer '{stepResult.StepName}' verfuegbar.");
            dataGrid.SelectedItem = null;
            return;
        }

        var window = new ConnectionGraphWindow(stepResult.StepName, stepResult.Traces, stepResult.CurvePoints) { Owner = this };
        window.ShowDialog();
        dataGrid.SelectedItem = null;
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

        _liveStateWindow.UpdateSnapshot(_timeline[_timelineIndex].Snapshot, _signalHistory);
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
                step.CurvePoints))
            .ToList();

        var logs = Logs
            .Select(log => new SimulationExportLogEntry(log.Timestamp, log.Message))
            .ToList();

        return new SimulationExportDocument(DateTimeOffset.Now, ConfigurationSummary, steps, logs);
    }
}
