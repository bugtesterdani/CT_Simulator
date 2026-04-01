// Provides Main Window Timeline for the desktop application support code.
using System.Collections.Generic;
using System.Globalization;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Executes OnContinueStep.
    /// </summary>
    private void OnContinueStep(object sender, System.Windows.RoutedEventArgs e)
    {
        if (CanStepForward)
        {
            SelectTimelineIndex(_timelineIndex + 1);
            return;
        }

        _stepGate.Set();
    }

    /// <summary>
    /// Executes OnResumeAutomaticRun.
    /// </summary>
    private void OnResumeAutomaticRun(object sender, System.Windows.RoutedEventArgs e)
    {
        IsStepModeEnabled = false;
        _pauseAtNextStep = false;
        _stepGate.Set();
    }

    /// <summary>
    /// Executes OnPauseAtNextStep.
    /// </summary>
    private void OnPauseAtNextStep(object sender, System.Windows.RoutedEventArgs e)
    {
        _pauseAtNextStep = true;
        AddLog("Simulation pausiert am naechsten Snapshot.");
    }

    /// <summary>
    /// Executes OnStepBack.
    /// </summary>
    private void OnStepBack(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!CanStepBackward)
        {
            return;
        }

        SelectTimelineIndex(_timelineIndex - 1);
    }

    /// <summary>
    /// Executes AppendTimelineSnapshot.
    /// </summary>
    private void AppendTimelineSnapshot(SimulationStateSnapshot snapshot)
    {
        var last = _timeline.Count == 0 ? null : _timeline[^1].Snapshot;
        if (last != null &&
            last.CurrentTimeMs == snapshot.CurrentTimeMs &&
            string.Equals(last.CurrentStep, snapshot.CurrentStep, System.StringComparison.Ordinal) &&
            AreMapsEqual(last.Signals, snapshot.Signals) &&
            AreMapsEqual(last.Variables, snapshot.Variables) &&
            AreMapsEqual(last.ExternalDeviceState.Outputs, snapshot.ExternalDeviceState.Outputs) &&
            AreMapsEqual(last.ExternalDeviceState.InternalSignals, snapshot.ExternalDeviceState.InternalSignals))
        {
            return;
        }

        var entry = new SimulationTimelineEntry(_timeline.Count, snapshot);
        _timeline.Add(entry);
        TimelineEntries.Add(entry);
        RefreshTimelineAnnotations();
        _timelineIndex = _timeline.Count - 1;
        _selectedTimelineEntry = entry;
        OnPropertyChanged(nameof(SelectedTimelineEntry));
        AppendSignalHistory(snapshot);
        RaiseTimelineNavigationChanged();
    }

    /// <summary>
    /// Executes AppendSignalHistory.
    /// </summary>
    private void AppendSignalHistory(SimulationStateSnapshot snapshot)
    {
        foreach (var sample in EnumerateSignalHistoryPoints(snapshot))
        {
            if (!_signalHistory.TryGetValue(sample.Label, out var points))
            {
                points = new List<MeasurementCurvePoint>();
                _signalHistory[sample.Label] = points;
            }

            if (points.Count > 0 && points[^1].TimeMs == sample.TimeMs && System.Nullable.Equals(points[^1].Value, sample.Value))
            {
                continue;
            }

            points.Add(sample);
        }
    }

    /// <summary>
    /// Executes EnumerateSignalHistoryPoints.
    /// </summary>
    private IEnumerable<MeasurementCurvePoint> EnumerateSignalHistoryPoints(SimulationStateSnapshot snapshot)
    {
        foreach (var item in snapshot.Signals)
        {
            if (TryParseNumeric(item.Value, out var numeric))
            {
                yield return new MeasurementCurvePoint(snapshot.CurrentTimeMs, $"SYS {item.Key}", numeric);
            }
        }

        foreach (var item in snapshot.ExternalDeviceState.Inputs)
        {
            if (TryParseNumeric(item.Value, out var numeric))
            {
                yield return new MeasurementCurvePoint(snapshot.CurrentTimeMs, $"DUT IN {item.Key}", numeric);
            }
        }

        foreach (var item in snapshot.ExternalDeviceState.Sources)
        {
            if (TryParseNumeric(item.Value, out var numeric))
            {
                yield return new MeasurementCurvePoint(snapshot.CurrentTimeMs, $"DUT SRC {item.Key}", numeric);
            }
        }

        foreach (var item in snapshot.ExternalDeviceState.InternalSignals)
        {
            if (TryParseNumeric(item.Value, out var numeric))
            {
                yield return new MeasurementCurvePoint(snapshot.CurrentTimeMs, $"DUT INT {item.Key}", numeric);
            }
        }

        foreach (var item in snapshot.ExternalDeviceState.Outputs)
        {
            if (TryParseNumeric(item.Value, out var numeric))
            {
                yield return new MeasurementCurvePoint(snapshot.CurrentTimeMs, $"DUT OUT {item.Key}", numeric);
            }
        }
    }

    /// <summary>
    /// Executes TryParseNumeric.
    /// </summary>
    private static bool TryParseNumeric(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    /// <summary>
    /// Executes AreMapsEqual.
    /// </summary>
    private static bool AreMapsEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var item in left)
        {
            if (!right.TryGetValue(item.Key, out var other) || !string.Equals(item.Value, other, System.StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Executes RaiseTimelineNavigationChanged.
    /// </summary>
    private void RaiseTimelineNavigationChanged()
    {
        OnPropertyChanged(nameof(CanStepBackward));
        OnPropertyChanged(nameof(CanStepForward));
    }

    /// <summary>
    /// Executes BuildSignalHistoryUpToTimelineIndex.
    /// </summary>
    private IReadOnlyDictionary<string, List<MeasurementCurvePoint>> BuildSignalHistoryUpToTimelineIndex(int index)
    {
        var history = new Dictionary<string, List<MeasurementCurvePoint>>(StringComparer.OrdinalIgnoreCase);
        if (index < 0)
        {
            return history;
        }

        var upper = Math.Min(index, _timeline.Count - 1);
        for (var i = 0; i <= upper; i++)
        {
            foreach (var sample in EnumerateSignalHistoryPoints(_timeline[i].Snapshot))
            {
                if (!history.TryGetValue(sample.Label, out var points))
                {
                    points = new List<MeasurementCurvePoint>();
                    history[sample.Label] = points;
                }

                if (points.Count > 0 && points[^1].TimeMs == sample.TimeMs && Nullable.Equals(points[^1].Value, sample.Value))
                {
                    continue;
                }

                points.Add(sample);
            }
        }

        return history;
    }

    /// <summary>
    /// Executes SelectTimelineIndex.
    /// </summary>
    private void SelectTimelineIndex(int index, bool keepSelection = false)
    {
        if (index < 0 || index >= _timeline.Count)
        {
            return;
        }

        _timelineIndex = index;
        var selected = _timeline[index];
        if (!keepSelection || !ReferenceEquals(_selectedTimelineEntry, selected))
        {
            _selectedTimelineEntry = selected;
            OnPropertyChanged(nameof(SelectedTimelineEntry));
        }

        CurrentStep = selected.Snapshot.CurrentStep;
        RebuildStepTreeForTimelineIndex(index);
        SelectBestStepNodeForTimelineIndex(index, selected.Snapshot.CurrentStep);
        if (_evaluationDetailsWindow != null && SelectedStepTreeNode?.Result != null)
        {
            _evaluationDetailsWindow.UpdateResult(SelectedStepTreeNode.Result);
        }
        RaiseTimelineNavigationChanged();
        UpdateLiveStateWindow();
    }

    /// <summary>
    /// Executes RefreshTimelineAnnotations.
    /// </summary>
    private void RefreshTimelineAnnotations()
    {
        foreach (var entry in _timeline)
        {
            entry.ResultSourceLabel = string.Empty;
            entry.ComparisonLabel = string.Empty;
        }

        foreach (var result in StepResults)
        {
            if (!result.TimelineIndex.HasValue)
            {
                continue;
            }

            var index = result.TimelineIndex.Value;
            if (index < 0 || index >= _timeline.Count)
            {
                continue;
            }

            _timeline[index].ResultSourceLabel = result.ResultSourceLabel;
            if (result.HasComparisonSummary)
            {
                _timeline[index].ComparisonLabel = result.ComparisonSummary;
            }
        }
    }
}
