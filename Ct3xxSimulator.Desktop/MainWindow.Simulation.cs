// Provides Main Window Simulation for the desktop application support code.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    private async void OnStartSimulation(object sender, RoutedEventArgs e)
    {
        await StartSimulationAsync();
    }

    private void OnCancelSimulation(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private async Task StartSimulationAsync(int? replayPauseAfterStepCount = null)
    {
        ResolveProgramFromCurrentFolder(true);
        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !LoadProgramFile(SelectedFilePath, true))
        {
            return;
        }

        var validationIssues = ValidateCurrentConfiguration(false);
        if (validationIssues.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validationIssues), "Validierung fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StepResults.Clear();
        StepTreeRootNodes.Clear();
        _stepEvaluationHistory.Clear();
        Logs.Clear();
        CurrentStep = null;
        _latestStateSnapshot = null;
        _timeline.Clear();
        TimelineEntries.Clear();
        _signalHistory.Clear();
        _timelineIndex = -1;
        _isLoadedSnapshotSession = false;
        SelectedTimelineEntry = null;
        _executedTestCount = 0;
        _replayPauseAfterStepCount = replayPauseAfterStepCount;
        UpdateLiveStateWindow();
        IsSimulationRunning = true;
        SimulationRunStateText = "Laeuft";
        SimulationRunStateBrush = System.Windows.Media.Brushes.SeaGreen;
        _cts = new CancellationTokenSource();

        try
        {
            var loadedProgram = _program;
            if (loadedProgram == null)
            {
                return;
            }

            Dispatcher.Invoke(() => BuildStepTree(loadedProgram));

            ApplySimulationOverrides();
            EnsurePythonDevice();

            await Task.Run(() =>
            {
                var simulator = new Ct3xxProgramSimulator(this, this, this);
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
            if (_suppressCancellationLogOnce)
            {
                _suppressCancellationLogOnce = false;
            }
            else
            {
                AddLog("Simulation abgebrochen.");
            }
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
            _replayPauseAfterStepCount = null;
            _stepGate.Set();
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
            throw new InvalidOperationException("Das gewaehlte Geraetemodell konnte nicht gestartet werden.");
        }

        _previousPythonPipe = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE", _pythonDeviceHost.PipePath, EnvironmentVariableTarget.Process);
        AddLog($"Geraetemodell gestartet: {Path.GetFileName(PythonScriptPath)}");
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
        _liveStateWindow?.Close();
        _evaluationDetailsWindow?.Close();
        DisposePythonDeviceHost();
        RestoreSimulationOverrides();
    }

    /// <summary>
    /// Executes wait before test.
    /// </summary>
    public void WaitBeforeTest(Test test, CancellationToken cancellationToken)
    {
    }

    /// <summary>
    /// Executes wait after test.
    /// </summary>
    public void WaitAfterTest(Test test, CancellationToken cancellationToken)
    {
        _executedTestCount++;
        if (_breakpointTests.Contains(test))
        {
            var label = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
            PauseExecutionAtInteractionPoint($"Breakpoint erreicht: {label}", cancellationToken);
        }
    }

    /// <summary>
    /// Executes wait after group.
    /// </summary>
    public void WaitAfterGroup(Group group, CancellationToken cancellationToken)
    {
        if (_breakpointGroups.Contains(group))
        {
            var label = group.Name ?? group.Id ?? "Gruppe";
            PauseExecutionAtInteractionPoint($"Gruppen-Breakpoint erreicht: {label}", cancellationToken);
        }
    }

    /// <summary>
    /// Executes wait after snapshot.
    /// </summary>
    public void WaitAfterSnapshot(SimulationStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        var shouldPause =
            IsStepModeEnabled ||
            _pauseAtNextStep ||
            (_replayPauseAfterStepCount.HasValue && _executedTestCount >= _replayPauseAfterStepCount.Value);

        if (!shouldPause)
        {
            return;
        }

        _pauseAtNextStep = false;
        if (_replayPauseAfterStepCount.HasValue && _executedTestCount >= _replayPauseAfterStepCount.Value)
        {
            PauseExecutionAtInteractionPoint($"Navigation an Snapshot {_timelineIndex + 1} angehalten.", cancellationToken, addLog: false);
            _replayPauseAfterStepCount = null;
        }
        else
        {
            PauseExecutionAtInteractionPoint($"Pause an Snapshot {_timelineIndex + 1}: {snapshot.ConcurrentEvent ?? snapshot.CurrentStep ?? "-"}", cancellationToken, addLog: false);
        }
    }

    private void PauseExecutionAtInteractionPoint(string message, CancellationToken cancellationToken, bool addLog = true)
    {
        if (addLog)
        {
            AddLog(message);
        }

        Dispatcher.Invoke(() =>
        {
            SimulationRunStateText = "Pausiert";
            SimulationRunStateBrush = System.Windows.Media.Brushes.DarkOrange;
        });

        _stepGate.Reset();
        while (!_stepGate.Wait(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        Dispatcher.Invoke(() =>
        {
            if (IsSimulationRunning)
            {
                SimulationRunStateText = "Laeuft";
                SimulationRunStateBrush = System.Windows.Media.Brushes.SeaGreen;
            }
        });
    }
}
