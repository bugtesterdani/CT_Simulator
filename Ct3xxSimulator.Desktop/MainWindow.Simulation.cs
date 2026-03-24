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
        Logs.Clear();
        CurrentStep = null;
        _latestStateSnapshot = null;
        _timeline.Clear();
        _signalHistory.Clear();
        _timelineIndex = -1;
        _executedTestCount = 0;
        _replayPauseAfterStepCount = replayPauseAfterStepCount;
        UpdateLiveStateWindow();
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
        DisposePythonDeviceHost();
        RestoreSimulationOverrides();
    }

    public void WaitBeforeTest(Test test, CancellationToken cancellationToken)
    {
    }

    public void WaitAfterTest(Test test, CancellationToken cancellationToken)
    {
        _executedTestCount++;

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
            AddLog($"Replay an Schritt {_executedTestCount} angehalten.");
            _replayPauseAfterStepCount = null;
        }
        else
        {
            AddLog($"Pause nach Testschritt: {test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test"}");
        }

        _stepGate.Reset();
        while (!_stepGate.Wait(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
