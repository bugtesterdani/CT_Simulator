// Provides Main Window Presentation for the desktop application support code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Win32;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Desktop.Views;
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;
using Ct3xxSimulator.Simulation.WireViz;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Executes AddLog.
    /// </summary>
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

    /// <summary>
    /// Adds the log.
    /// </summary>
    public void OnProgramStarted(Ct3xxProgram program) => AddLog($"Programm gestartet: {program.ProgramVersion ?? program.Id ?? "unbekannt"}");
    /// <summary>
    /// Adds the log.
    /// </summary>
    public void OnLoopIteration(int iteration, int totalIterations) => AddLog($"DUT-Durchlauf {iteration}/{totalIterations}");
    /// <summary>
    /// Executes on group started.
    /// </summary>
    public void OnGroupStarted(Group group)
    {
        Dispatcher.Invoke(() => SetGroupExpanded(group, true));
        AddLog($"Gruppe: {group.Name}");
    }
    /// <summary>
    /// Adds the log.
    /// </summary>
    public void OnGroupSkipped(Group group, string reason) => AddLog($"Gruppe uebersprungen: {group.Name} ({reason})");
    /// <summary>
    /// Executes on group completed.
    /// </summary>
    public void OnGroupCompleted(Group group)
    {
        Dispatcher.Invoke(() => SetGroupExpanded(group, false));
    }

    /// <summary>
    /// Executes on test started.
    /// </summary>
    public void OnTestStarted(Test test)
    {
        CurrentStep = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        AddLog($"Starte: {CurrentStep}");
    }

    /// <summary>
    /// Executes on test completed.
    /// </summary>
    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        AddLog($"Ergebnis {CurrentStep}: {outcome.ToString().ToUpperInvariant()}");
    }

    /// <summary>
    /// Executes on step evaluated.
    /// </summary>
    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        Dispatcher.Invoke(() =>
        {
            var csvMatch = TryConsumeCsvReplayMatch(test, evaluation);
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
                _timeline.Count == 0 ? null : _timeline.Count - 1,
                csvMatch?.CsvStep.RowNumber,
                csvMatch?.CsvStep.Description,
                csvMatch?.CsvStep.Message,
                csvMatch?.CsvStep.Result,
                csvMatch?.CsvStep.RawMeasuredValue,
                csvMatch?.CsvStep.RawLowerLimit,
                csvMatch?.CsvStep.RawUpperLimit,
                csvMatch?.Reason,
                SelectedCsvReplayMode.ToString(),
                test.LogFlags,
                IsCsvLogExpectedForOutcome(test.LogFlags, evaluation.Outcome));
            StepResults.Add(result);
            _stepEvaluationHistory.Add(new StepEvaluationHistoryEntry(test, result));
            ApplyEvaluationToStepTree(test, result);
            RefreshTimelineAnnotations();
        });
    }

    /// <summary>
    /// Executes on state changed.
    /// </summary>
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

    /// <summary>
    /// Executes OnStepTreeSelected.
    /// </summary>
    private void OnStepTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SelectedStepTreeNode = e.NewValue as StepTreeNodeViewModel;
    }

    /// <summary>
    /// Executes OnStepTreeDoubleClick.
    /// </summary>
    private void OnStepTreeDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var sourceItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (sourceItem == null || !ReferenceEquals(sender, sourceItem))
        {
            return;
        }

        var node = sourceItem?.DataContext as StepTreeNodeViewModel ?? SelectedStepTreeNode;
        if (node is null)
        {
            return;
        }

        if (TryOpenMeasurementOverviewForNode(node))
        {
            e.Handled = true;
            return;
        }

        var stepResult = ResolveTraceResultForNode(node);
        var traces = stepResult?.Traces;
        string? failureReason = null;
        if (traces == null || traces.Count == 0)
        {
            traces = BuildFallbackTracesForNode(node, out failureReason);
        }

        if (traces == null || traces.Count == 0)
        {
            var reasonText = string.IsNullOrWhiteSpace(failureReason)
                ? $"Keine Verbindungsansicht fuer '{node.Title}' verfuegbar."
                : $"Keine Verbindungsansicht fuer '{node.Title}' verfuegbar. Grund: {failureReason}";
            AddLog(reasonText);
            MessageBox.Show(this, reasonText, "Verdrahtung nicht verfuegbar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new ConnectionGraphWindow(
            stepResult?.StepName ?? node.Title,
            traces,
            stepResult?.CurvePoints) { Owner = this };
        window.ShowDialog();
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Executes ResolveTraceResultForNode.
    /// </summary>
    private StepResultViewModel? ResolveTraceResultForNode(StepTreeNodeViewModel node)
    {
        if (node.Result != null && node.Result.Traces.Count > 0)
        {
            return node.Result;
        }

        if (_treeNodeTests.TryGetValue(node, out var test))
        {
            var match = _stepEvaluationHistory
                .Where(item => ReferenceEquals(item.Test, test) && item.Result.Traces.Count > 0)
                .Select(item => item.Result)
                .LastOrDefault();
            if (match != null)
            {
                return match;
            }
        }

        var childWithTrace = node.Children
            .Select(child => ResolveTraceResultForNode(child))
            .FirstOrDefault(result => result != null && result.Traces.Count > 0);
        return childWithTrace;
    }

    /// <summary>
    /// Executes BuildFallbackTracesForNode.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace>? BuildFallbackTracesForNode(StepTreeNodeViewModel node, out string? reason)
    {
        reason = null;
        if (!_treeNodeTests.TryGetValue(node, out var test))
        {
            reason = "Kein zugehoeriger Testschritt gefunden.";
            return null;
        }

        var resolver = GetWireVizResolver();
        if (resolver == null)
        {
            reason = BuildResolverFailureReason();
            return null;
        }

        var signalState = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var candidates = CollectSignalCandidates(test, signalState);
        if (candidates.Count == 0)
        {
            reason = "Im Testschritt wurden keine Signal-/Verdrahtungsreferenzen gefunden.";
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (resolver.TryTrace(candidate, signalState, out var traces) && traces.Count > 0)
            {
                return traces
                    .Select(trace => new StepConnectionTrace($"Verdrahtung: {trace.SignalName}", trace.Nodes))
                    .ToList();
            }
        }

        var fallback = new List<StepConnectionTrace>();
        foreach (var candidate in candidates)
        {
            if (!resolver.TryResolve(candidate, out var resolutions))
            {
                continue;
            }

            foreach (var resolution in resolutions)
            {
                if (resolution.Targets.Count == 0)
                {
                    continue;
                }

                foreach (var target in resolution.Targets)
                {
                    fallback.Add(new StepConnectionTrace(
                        $"Verdrahtung: {resolution.Assignment.Name}",
                        new[]
                        {
                            resolution.Source.DisplayName,
                            target.DisplayName
                        }));
                }
            }
        }

        if (fallback.Count == 0)
        {
            reason = resolver.SignalCount == 0
                ? "WireViz wurde geladen, enthaelt aber keine Signale."
                : "Kein aktiver Pfad gefunden (Signal nicht im WireViz oder Relais offen).";
        }

        return fallback;
    }

    /// <summary>
    /// Executes BuildResolverFailureReason.
    /// </summary>
    private string BuildResolverFailureReason()
    {
        if (_fileSet == null)
        {
            return "Programm ist nicht geladen.";
        }

        if (string.IsNullOrWhiteSpace(WiringFolderPath))
        {
            return "Verdrahtungsordner ist nicht gesetzt.";
        }

        if (!Directory.Exists(WiringFolderPath))
        {
            return $"Verdrahtungsordner nicht gefunden: {WiringFolderPath}";
        }

        var yamlFiles = Directory.EnumerateFiles(WiringFolderPath, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(WiringFolderPath, "*.yaml", SearchOption.TopDirectoryOnly))
            .ToList();

        if (yamlFiles.Count == 0)
        {
            return "Keine WireViz-Dateien (*.yml/*.yaml) im Verdrahtungsordner gefunden.";
        }

        return "WireViz konnte nicht geladen werden.";
    }

    private WireVizHarnessResolver? GetWireVizResolver()
    {
        if (_fileSet == null)
        {
            return null;
        }

        var programPath = _fileSet.ProgramDirectory ?? string.Empty;
        var wireVizRoot = string.IsNullOrWhiteSpace(WiringFolderPath) ? null : Path.GetFullPath(WiringFolderPath);
        var simulationRoot = string.IsNullOrWhiteSpace(SimulationModelFolderPath) ? null : Path.GetFullPath(SimulationModelFolderPath);

        if (_wireVizResolver != null &&
            string.Equals(_wireVizResolverProgramPath, programPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_wireVizResolverWireVizRoot, wireVizRoot, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_wireVizResolverSimulationRoot, simulationRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _wireVizResolver;
        }

        var previousWireVizRoot = Environment.GetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", EnvironmentVariableTarget.Process);
        var previousSimulationRoot = Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", EnvironmentVariableTarget.Process);

        try
        {
            if (!string.IsNullOrWhiteSpace(wireVizRoot))
            {
                Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", wireVizRoot, EnvironmentVariableTarget.Process);
            }

            if (!string.IsNullOrWhiteSpace(simulationRoot))
            {
                Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", simulationRoot, EnvironmentVariableTarget.Process);
            }

            _wireVizResolver = WireVizHarnessResolver.Create(_fileSet);
            _wireVizResolverProgramPath = programPath;
            _wireVizResolverWireVizRoot = wireVizRoot;
            _wireVizResolverSimulationRoot = simulationRoot;
            return _wireVizResolver;
        }
        catch
        {
            _wireVizResolver = null;
            _wireVizResolverProgramPath = null;
            _wireVizResolverWireVizRoot = null;
            _wireVizResolverSimulationRoot = null;
            return null;
        }
        finally
        {
            Environment.SetEnvironmentVariable("CT3XX_WIREVIZ_ROOT", previousWireVizRoot, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT", previousSimulationRoot, EnvironmentVariableTarget.Process);
        }
    }

    private static IReadOnlyList<string> CollectSignalCandidates(Test test, IDictionary<string, object?> signalState)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parameters = test.Parameters;

        void AddCandidate(string? raw)
        {
            var normalized = ExtractSignalName(raw);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
            }
        }

        if (parameters != null)
        {
            AddCandidate(parameters.DrawingReference);
            AddCandidate(parameters.Message);

            foreach (var record in parameters.Records)
            {
                AddCandidate(record.DrawingReference);
                AddCandidate(record.TestPoint);
                AddCandidate(record.Text);
                AddCandidate(record.Expression);
                AddCandidate(record.Destination);
                AddRecordAttributes(record, candidates, signalState);
            }

            foreach (var table in parameters.Tables)
            {
                foreach (var record in table.Records)
                {
                    AddCandidate(record.DrawingReference);
                    AddCandidate(record.TestPoint);
                    AddCandidate(record.Text);
                    AddCandidate(record.Expression);
                    AddCandidate(record.Destination);
                    AddRecordAttributes(record, candidates, signalState);
                }
            }

            if (parameters.ExtraElements != null)
            {
                foreach (var element in parameters.ExtraElements)
                {
                    AddSignalFromXmlElement(element, candidates, signalState);
                }
            }
        }

        return candidates.ToList();
    }

    private static void AddSignalFromXmlElement(XmlElement element, ISet<string> candidates, IDictionary<string, object?> signalState)
    {
        if (element == null)
        {
            return;
        }

        var channelName = element.GetAttribute("ChannelName");
        var normalized = ExtractSignalName(channelName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            candidates.Add(normalized);

            var relayState = element.GetAttribute("RelayState");
            var outState = element.GetAttribute("Out");
            var value = ResolvePreviewSignalValue(relayState, outState);
            if (value != null)
            {
                signalState[normalized] = value;
            }
        }

        AddOptionalSignalAttribute(element, candidates, "Signal");
        AddOptionalSignalAttribute(element, candidates, "Source");
        AddOptionalSignalAttribute(element, candidates, "Target");
        AddOptionalSignalAttribute(element, candidates, "SCL");
        AddOptionalSignalAttribute(element, candidates, "SDA");
        AddOptionalSignalAttribute(element, candidates, "MOSI");
        AddOptionalSignalAttribute(element, candidates, "MISO");
        AddOptionalSignalAttribute(element, candidates, "CLK");
        AddOptionalSignalAttribute(element, candidates, "CS");
        AddOptionalSignalAttribute(element, candidates, "CSST");
        AddOptionalSignalAttribute(element, candidates, "UIFSignal");
    }

    private static void AddRecordAttributes(Record record, ISet<string> candidates, IDictionary<string, object?> signalState)
    {
        if (record.AdditionalAttributes == null || record.AdditionalAttributes.Length == 0)
        {
            return;
        }

        var channelName = ReadAttributeValue(record.AdditionalAttributes, "ChannelName");
        var normalized = ExtractSignalName(channelName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            candidates.Add(normalized);

            var relayState = ReadAttributeValue(record.AdditionalAttributes, "RelayState");
            var outState = ReadAttributeValue(record.AdditionalAttributes, "Out");
            var value = ResolvePreviewSignalValue(relayState, outState);
            if (value != null)
            {
                signalState[normalized] = value;
            }
        }

        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "Signal");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "Source");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "Target");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "SCL");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "SDA");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "MOSI");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "MISO");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "CLK");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "CS");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "CSST");
        AddOptionalSignalAttribute(record.AdditionalAttributes, candidates, "UIFSignal");
    }

    private static void AddOptionalSignalAttribute(XmlAttribute[] attributes, ISet<string> candidates, string attributeName)
    {
        var raw = ReadAttributeValue(attributes, attributeName);
        var normalized = ExtractSignalName(raw);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            candidates.Add(normalized);
        }
    }

    private static string? ReadAttributeValue(XmlAttribute[] attributes, string name)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static void AddOptionalSignalAttribute(XmlElement element, ISet<string> candidates, string attributeName)
    {
        var raw = element.GetAttribute(attributeName);
        var normalized = ExtractSignalName(raw);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            candidates.Add(normalized);
        }
    }

    private static object? ResolvePreviewSignalValue(string? relayState, string? outState)
    {
        var relay = relayState?.Trim().Trim('\'', '"');
        if (!string.IsNullOrWhiteSpace(relay))
        {
            if (relay.Equals("On", StringComparison.OrdinalIgnoreCase) ||
                relay.Equals("UnverifiedOn", StringComparison.OrdinalIgnoreCase))
            {
                return 24d;
            }

            if (relay.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                relay.Equals("UnverifiedOff", StringComparison.OrdinalIgnoreCase))
            {
                return 0d;
            }
        }

        var outValue = outState?.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(outValue))
        {
            return null;
        }

        return outValue.ToUpperInvariant() switch
        {
            "H" => 24d,
            "L" => 0d,
            "1" => 24d,
            "0" => 0d,
            "TRUE" => true,
            "FALSE" => false,
            _ => null
        };
    }

    private static string? ExtractSignalName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().Trim('\'', '"');
        const string prefix = "SIG:";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..].Trim();
        }

        return trimmed;
    }

    private void ShowEvaluationDetails(StepResultViewModel result)
    {
        if (_evaluationDetailsWindow == null)
        {
            _evaluationDetailsWindow = new EvaluationDetailsWindow { Owner = this };
            _evaluationDetailsWindow.Closed += (_, _) => _evaluationDetailsWindow = null;
        }

        _evaluationDetailsWindow.UpdateResult(result);
        _evaluationDetailsWindow.Show();
        _evaluationDetailsWindow.Activate();
    }

    private void OnShowEvaluationDetails(object sender, RoutedEventArgs e)
    {
        if (SelectedStepTreeNode?.Result == null)
        {
            AddLog("Fuer den ausgewaehlten Testschritt ist keine Auswertungsanalyse verfuegbar.");
            return;
        }

        ShowEvaluationDetails(SelectedStepTreeNode.Result);
    }

    private List<MeasurementEntryViewModel> BuildMeasurementEntries()
    {
        var entries = new List<MeasurementEntryViewModel>();
        foreach (var entry in _stepEvaluationHistory)
        {
            var testType = entry.Test?.Id?.Trim();
            if (string.IsNullOrWhiteSpace(testType))
            {
                continue;
            }

            testType = testType.ToUpperInvariant();
            if (testType is not ("ICT" or "CTCT" or "SHRT"))
            {
                continue;
            }

            var result = entry.Result;
            entries.Add(new MeasurementEntryViewModel(
                testType,
                result.StepName,
                result.Outcome,
                result.MeasuredValue,
                result.LowerLimit,
                result.UpperLimit,
                result.Unit,
                BuildContactSummary(result),
                result.Details,
                result.Traces,
                result.CurvePoints));
        }

        return entries;
    }

    private static string BuildContactSummary(StepResultViewModel result)
    {
        if (result.Traces.Count == 0)
        {
            return "-";
        }

        var contacts = new List<string>();
        foreach (var trace in result.Traces)
        {
            if (trace == null)
            {
                continue;
            }

            if (TryParseContact(trace.Title, out var contact))
            {
                contacts.Add(contact);
            }
            else if (!string.IsNullOrWhiteSpace(trace.Title))
            {
                contacts.Add(trace.Title);
            }
        }

        var distinct = contacts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return distinct.Count == 0 ? "-" : string.Join(", ", distinct);
    }

    private static bool TryParseContact(string? title, out string contact)
    {
        contact = string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = title.Trim();
        if (normalized.StartsWith("CTCT:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("SHRT:", StringComparison.OrdinalIgnoreCase))
        {
            var separator = normalized.IndexOf(':');
            var arrowIndex = normalized.IndexOf("->", StringComparison.Ordinal);
            if (separator > 0 && arrowIndex > separator)
            {
                var left = normalized[(separator + 1)..arrowIndex].Trim();
                var right = normalized[(arrowIndex + 2)..].Trim();
                var bracket = right.IndexOf("(", StringComparison.Ordinal);
                if (bracket > 0)
                {
                    right = right[..bracket].Trim();
                }

                if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
                {
                    contact = $"{left} -> {right}";
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryOpenMeasurementOverviewForNode(StepTreeNodeViewModel node)
    {
        var test = GetTestForNode(node);
        if (test == null)
        {
            return false;
        }

        var testType = test.Id?.Trim();
        if (string.IsNullOrWhiteSpace(testType))
        {
            return false;
        }

        testType = testType.ToUpperInvariant();
        if (testType is not ("ICT" or "CTCT" or "SHRT"))
        {
            return false;
        }

        try
        {
            var entries = BuildMeasurementEntries();
            var preferredStep = node.Title;
            var window = new MeasurementOverviewWindow(entries, testType, preferredStep) { Owner = this };
            window.Show();
            window.Activate();
            return true;
        }
        catch (Exception ex)
        {
            var reasonText = $"Messuebersicht konnte nicht geoeffnet werden. Grund: {ex.Message}";
            AddLog(reasonText);
            MessageBox.Show(this, reasonText, "Messuebersicht", MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }

    private Test? GetTestForNode(StepTreeNodeViewModel node)
    {
        var current = node;
        while (current != null)
        {
            if (_treeNodeTests.TryGetValue(current, out var test))
            {
                return test;
            }

            current = current.Parent;
        }

        return null;
    }

    private void OnToggleBreakpoint(object sender, RoutedEventArgs e)
    {
        if (!CanToggleBreakpoint)
        {
            AddLog("Breakpoints koennen nur auf echten Testschritten gesetzt werden.");
            return;
        }

        ToggleBreakpointForSelectedNode();
    }

    /// <summary>
    /// Adds the log.
    /// </summary>
    public void OnMessage(string message) => AddLog(message);

    /// <summary>
    /// Executes prompt selection.
    /// </summary>
    public string PromptSelection(string message, IReadOnlyList<string> options)
    {
        return Dispatcher.Invoke(() =>
        {
            var dialog = new SelectionDialog(this, message, options);
            return dialog.ShowDialog() == true ? dialog.SelectedOption : options.Count > 0 ? options[0] : string.Empty;
        });
    }

    /// <summary>
    /// Executes prompt input.
    /// </summary>
    public string PromptInput(string prompt)
    {
        return Dispatcher.Invoke(() =>
        {
            var dialog = new InputDialog(this, prompt);
            return dialog.ShowDialog() == true ? dialog.Response : string.Empty;
        });
    }

    /// <summary>
    /// Executes prompt input.
    /// </summary>
    public string PromptMeasurement(Test test, Record record, string prompt, string? unit) => PromptInput(prompt);

    /// <summary>
    /// Executes prompt pass fail.
    /// </summary>
    public bool PromptPassFail(string message)
    {
        return Dispatcher.Invoke(() => MessageBox.Show(this, message, "Operator", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
    }

    /// <summary>
    /// Executes show message.
    /// </summary>
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
                step.TimelineIndex,
                step.CsvRowNumber,
                step.CsvDescription,
                step.CsvMessage,
                step.CsvOutcome,
                step.CsvMeasuredValue,
                step.CsvLowerLimit,
                step.CsvUpperLimit,
                step.CsvMatchReason,
                step.CsvDisplayMode,
                step.CsvLogFlags,
                step.CsvLogExpectedForOutcome))
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
                step.TimelineIndex,
                step.CsvRowNumber,
                step.CsvDescription,
                step.CsvMessage,
                step.CsvOutcome,
                step.CsvMeasuredValue,
                step.CsvLowerLimit,
                step.CsvUpperLimit,
                step.CsvMatchReason,
                step.CsvDisplayMode,
                step.CsvLogFlags,
                step.CsvLogExpectedForOutcome);
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
        RefreshTimelineAnnotations();

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

    private ImportedTestRunStepMatch? TryConsumeCsvReplayMatch(Test test, StepEvaluation evaluation)
    {
        if (_activeCsvReplayMatches.Count == 0 || _csvReplayMatchCursor >= _activeCsvReplayMatches.Count)
        {
            return null;
        }

        if (IsCsvMatchCompatible(_activeCsvReplayMatches[_csvReplayMatchCursor], test, evaluation))
        {
            return _activeCsvReplayMatches[_csvReplayMatchCursor++];
        }

        var searchLimit = Math.Min(_activeCsvReplayMatches.Count, _csvReplayMatchCursor + 4);
        for (var index = _csvReplayMatchCursor + 1; index < searchLimit; index++)
        {
            if (!IsCsvMatchCompatible(_activeCsvReplayMatches[index], test, evaluation))
            {
                continue;
            }

            _csvReplayMatchCursor = index + 1;
            return _activeCsvReplayMatches[index];
        }

        return null;
    }

    private static bool IsCsvMatchCompatible(ImportedTestRunStepMatch match, Test test, StepEvaluation evaluation)
    {
        return ReferenceEquals(match.ProgramStep.SourceTest, test) &&
               string.Equals(match.ProgramStep.DisplayName, evaluation.StepName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCsvLogExpectedForOutcome(string? logFlags, TestOutcome outcome)
    {
        if (string.IsNullOrWhiteSpace(logFlags))
        {
            return true;
        }

        return outcome switch
        {
            TestOutcome.Pass => logFlags.IndexOf('P', StringComparison.OrdinalIgnoreCase) >= 0,
            TestOutcome.Fail => logFlags.IndexOf('F', StringComparison.OrdinalIgnoreCase) >= 0,
            TestOutcome.Error => logFlags.IndexOf('E', StringComparison.OrdinalIgnoreCase) >= 0,
            _ => true
        };
    }
}
