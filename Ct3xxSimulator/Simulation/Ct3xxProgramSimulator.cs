using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulator.Simulation.Devices;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxSimulator.Simulation;

public class Ct3xxProgramSimulator
{
    private readonly SimulationContext _context = new();
    private readonly ExpressionEvaluator _evaluator;
    private readonly IInteractionProvider _interaction;
    private readonly ISimulationObserver _observer;
    private ExternalDeviceSession? _externalDeviceSession;
    private WireVizHarnessResolver? _wireVizResolver;
    private bool _testerConfigurationAsked;
    private CancellationToken _cancellationToken;
    private readonly Dictionary<string, string> _measurementBusSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _signalState = new(StringComparer.OrdinalIgnoreCase);

    public Ct3xxProgramSimulator(IInteractionProvider? interactionProvider = null, ISimulationObserver? observer = null)
    {
        _interaction = interactionProvider ?? new ConsoleInteractionProvider();
        _observer = observer ?? new ConsoleSimulationObserver();
        _evaluator = new ExpressionEvaluator(_context);
    }

    public void Run(Ct3xxProgram program, int dutLoopIterations, CancellationToken cancellationToken = default)
    {
        _externalDeviceSession?.Dispose();
        _externalDeviceSession = null;
        _wireVizResolver = null;
        ResetSimulationState();
        RunCore(program, dutLoopIterations, cancellationToken);
    }

    public void Run(Ct3xxProgramFileSet fileSet, int dutLoopIterations, CancellationToken cancellationToken = default)
    {
        if (fileSet == null)
        {
            throw new ArgumentNullException(nameof(fileSet));
        }

        _externalDeviceSession?.Dispose();
        _externalDeviceSession = CreateExternalDeviceSession();
        _wireVizResolver = WireVizHarnessResolver.Create(fileSet);
        ResetSimulationState();
        RunCore(fileSet.Program, dutLoopIterations, cancellationToken);
    }

    private void RunCore(Ct3xxProgram program, int dutLoopIterations, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _observer.OnProgramStarted(program);
        if (_externalDeviceSession != null)
        {
            TryAnnounceExternalDevice();
        }

        if (_wireVizResolver?.SignalCount > 0)
        {
            _observer.OnMessage($"WireViz loaded: {_wireVizResolver.SignalCount} Signalzuordnungen gefunden.");
        }

        _context.ApplyTables(program.Tables, _evaluator);

        var loopExecuted = false;
        foreach (var item in program.RootItems)
        {
            CheckCancellation();
            if (!loopExecuted &&
                program.DutLoop != null &&
                item is Group marker &&
                string.Equals(marker.Id, "END$", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteDutLoop(program.DutLoop, dutLoopIterations);
                loopExecuted = true;
            }

            switch (item)
            {
                case Table table:
                    _context.ApplyTable(table, _evaluator);
                    break;
                case Test test:
                    ExecuteTest(test);
                    break;
                case Group group:
                    ExecuteGroup(group);
                    break;
            }
        }

        if (!loopExecuted && program.DutLoop != null)
        {
            ExecuteDutLoop(program.DutLoop, dutLoopIterations);
        }

        _observer.OnMessage($"Simulation finished. Last result: {_context.LastResult}");
    }

    private void ExecuteGroup(Group group)
    {
        if (IsDisabled(group.Disabled))
        {
            return;
        }

        EnsureConditionContext(group.ExecCondition);
        if (!_evaluator.EvaluateCondition(group.ExecCondition))
        {
            _observer.OnGroupSkipped(group, $"condition '{group.ExecCondition}' is FALSE");
            return;
        }

        _observer.OnGroupStarted(group);
        var repeatEnabled = !string.IsNullOrWhiteSpace(group.RepeatCondition);
        var iteration = 0;
        do
        {
            CheckCancellation();
            iteration++;
            foreach (var item in group.Items)
            {
                CheckCancellation();
                switch (item)
                {
                    case Table table:
                        _context.ApplyTable(table, _evaluator);
                        break;
                    case Test test:
                        ExecuteTest(test);
                        break;
                    case Group nested:
                        ExecuteGroup(nested);
                        break;
                }
            }
        }
        while (repeatEnabled && iteration < 10 && _evaluator.EvaluateCondition(group.RepeatCondition));

        if (repeatEnabled && iteration >= 10 && _evaluator.EvaluateCondition(group.RepeatCondition))
        {
            _observer.OnMessage("Repeat condition still TRUE after 10 iterations, aborting group.");
        }

        _observer.OnGroupCompleted(group);
    }

    private void ExecuteDutLoop(DutLoop loop, int iterations)
    {
        if (IsDisabled(loop.Disabled))
        {
            return;
        }

        EnsureConditionContext(loop.ExecCondition);
        if (!_evaluator.EvaluateCondition(loop.ExecCondition))
        {
            _observer.OnMessage("DUT loop skipped because condition is FALSE");
            return;
        }

        var runs = iterations <= 0 ? 1 : iterations;
        for (var run = 1; run <= runs; run++)
        {
            CheckCancellation();
            _observer.OnLoopIteration(run, runs);
            foreach (var item in loop.Items)
            {
                CheckCancellation();
                switch (item)
                {
                    case Table table:
                        _context.ApplyTable(table, _evaluator);
                        break;
                    case Test test:
                        ExecuteTest(test);
                        break;
                    case Group nested:
                        ExecuteGroup(nested);
                        break;
                }
            }
        }
    }

    private void ExecuteTest(Test test)
    {
        if (IsDisabled(test.Disabled))
        {
            return;
        }

        _observer.OnTestStarted(test);

        var outcome = test.Id?.ToUpperInvariant() switch
        {
            "GSD^" => RunAssignmentTest(test),
            "PET$" => RunEvaluationTest(test),
            "PRT^" => RunOperatorTest(test),
            "SC2C" => RunScannerConnectTest(test),
            "CDMA" => RunCdmaTest(test),
            _ => RunGenericTest(test)
        };

        _context.MarkOutcome(outcome);
        _observer.OnTestCompleted(test, outcome);
    }

    private TestOutcome RunAssignmentTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            return TestOutcome.Pass;
        }

        foreach (var table in parameters.Tables)
        {
            foreach (var record in table.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.Destination))
                {
                    continue;
                }

                var address = VariableAddress.From(record.Destination);
                var value = _evaluator.Evaluate(record.Expression);
                _context.SetValue(address, value);
                _observer.OnMessage($"{address} := {_evaluator.ToText(value)}");
                TryWriteExternalSignal(record.Destination, value);
            }
        }

        return TestOutcome.Pass;
    }

    private TestOutcome RunEvaluationTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            return TestOutcome.Pass;
        }

        var overall = TestOutcome.Pass;
        foreach (var table in parameters.Tables)
        {
            foreach (var record in table.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ReportWireViz(record.DrawingReference);
                ReportWireViz(record.Text);
                ReportWireViz(record.Expression);

                var externalValue = ResolveExternalMeasurement(record);
                var manualValue = externalValue ?? ResolveMeasurementInput(test, record);
                var value = manualValue ?? _evaluator.Evaluate(record.Expression);
                var measured = _evaluator.ToDouble(value);
                var lower = ParseNullable(record.LowerLimit);
                var upper = ParseNullable(record.UpperLimit);
                var pass = true;
                var label = record.DrawingReference ?? record.Expression ?? record.Id ?? "Measurement";
                var unit = record.Unit ?? string.Empty;
                string valueDisplay;

                if (!measured.HasValue)
                {
                    pass = false;
                    var textValue = _evaluator.ToText(value);
                    var fallback = string.IsNullOrWhiteSpace(textValue) ? "n/a" : textValue;
                    valueDisplay = $"{fallback} (invalid)";
                }
                else
                {
                    var numeric = measured.Value;
                    valueDisplay = numeric.ToString("0.###", CultureInfo.InvariantCulture);
                    if (lower.HasValue && numeric < lower.Value)
                    {
                        pass = false;
                    }

                    if (upper.HasValue && numeric > upper.Value)
                    {
                        pass = false;
                    }
                }

                var msg = $"{label}: {valueDisplay} {unit} (limits {lower?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-inf"} .. {upper?.ToString("0.###", CultureInfo.InvariantCulture) ?? "+inf"}) => {(pass ? "PASS" : "FAIL")}";
                _observer.OnMessage(msg);
                _observer.OnStepEvaluated(test, new StepEvaluation(label, pass ? TestOutcome.Pass : TestOutcome.Fail, measured, lower, upper, unit));
                if (!pass)
                {
                    overall = TestOutcome.Fail;
                }
            }
        }

        return overall;
    }

    private TestOutcome RunOperatorTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            return TestOutcome.Pass;
        }

        var mode = parameters.Mode ?? string.Empty;
        var message = _evaluator.ResolveText(parameters.Message);

        switch (mode)
        {
            case "Single selection":
                var options = _evaluator.ParseOptions(parameters.Options);
                var selection = _interaction.PromptSelection(message, options);
                if (!string.IsNullOrWhiteSpace(parameters.OptionsVariable))
                {
                    _context.SetValue(parameters.OptionsVariable, selection);
                }

                return TestOutcome.Pass;

            case "Input values into variables":
                foreach (var table in parameters.Tables)
                {
                    foreach (var record in table.Records)
                    {
                        if (string.IsNullOrWhiteSpace(record.Variable))
                        {
                            continue;
                        }

                        var prompt = record.Text ?? record.Variable;
                        var input = _interaction.PromptInput(prompt);
                        _context.SetValue(record.Variable, input);
                    }
                }

                return TestOutcome.Pass;

            case "Query for PASS/FAIL":
                var approved = _interaction.PromptPassFail(message);
                return approved ? TestOutcome.Pass : TestOutcome.Fail;

            case "Display a message":
                var requiresConfirmation = !string.Equals(parameters.AutoOk, "TRUE", StringComparison.OrdinalIgnoreCase);
                var displayText = string.IsNullOrWhiteSpace(message)
                    ? test.Parameters?.Name ?? "Display message"
                    : message;
                _observer.OnMessage(displayText);
                _interaction.ShowMessage(displayText, requiresConfirmation);
                return TestOutcome.Pass;

            case "Display results":
                var displayLines = new List<string>();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    displayLines.Add(message);
                    _observer.OnMessage(message);
                }

                var array = _context.GetArray(parameters.ResultArray);
                if (array != null)
                {
                    var entries = new List<string>();
                    var targets = ParseResultIndexes(parameters.NumberOfResults);
                    if (targets.Count == 0)
                    {
                        foreach (var entry in array.Snapshot())
                        {
                            var line = $"[{entry.Key}] {_evaluator.ToText(entry.Value)}";
                            entries.Add(line);
                            _observer.OnMessage(line);
                        }
                    }
                    else
                    {
                        foreach (var index in targets)
                        {
                            var value = array.Get(index);
                            var line = $"[{index}] {_evaluator.ToText(value)}";
                            entries.Add(line);
                            _observer.OnMessage(line);
                        }
                    }

                    if (entries.Count == 0)
                    {
                        entries.Add("Result array is empty.");
                        _observer.OnMessage("Result array is empty.");
                    }

                    displayLines.AddRange(entries);
                }
                else
                {
                    var info = "Result array is empty.";
                    displayLines.Add(info);
                    _observer.OnMessage(info);
                }

                var requiresConfirm = !string.Equals(parameters.AutoOk, "TRUE", StringComparison.OrdinalIgnoreCase);
                var windowText = string.Join(Environment.NewLine, displayLines);
                _interaction.ShowMessage(windowText, requiresConfirm);
                return TestOutcome.Pass;

            default:
                _observer.OnMessage($"Unsupported operator mode '{mode}'.");
                return TestOutcome.Pass;
        }
    }

    private TestOutcome RunGenericTest(Test test)
    {
        var info = test.Parameters?.DrawingReference ?? test.Parameters?.Message;
        ReportWireViz(info);
        if (!string.IsNullOrWhiteSpace(info))
        {
            _observer.OnMessage(info);
        }

        PublishStepEvaluation(test, TestOutcome.Pass, details: string.IsNullOrWhiteSpace(info) ? null : info);
        return TestOutcome.Pass;
    }

    private TestOutcome RunScannerConnectTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters?.AdditionalAttributes != null)
        {
            foreach (var attribute in parameters.AdditionalAttributes)
            {
                if (!attribute.Name.StartsWith("MBus", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var signal = ExtractSignalName(attribute.Value);
                if (!string.IsNullOrWhiteSpace(signal))
                {
                    _measurementBusSignals[attribute.Name] = signal!;
                }
            }
        }

        var details = _measurementBusSignals.Count == 0
            ? "Messbusse verbunden."
            : string.Join(", ", _measurementBusSignals.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"));
        _observer.OnMessage($"SC2C: {details}");
        RememberSignal("VCC_Plus", 24d);
        TrySetExternalSignal("HELPER_SUPPLY", true);
        PublishStepEvaluation(test, TestOutcome.Pass, details: details);
        return TestOutcome.Pass;
    }

    private TestOutcome RunCdmaTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "CDMA ohne Parameter.");
            return TestOutcome.Error;
        }

        var inputSignal = ResolveMeasurementBusSignal("MBus3");
        var outputSignal = ResolveMeasurementBusSignal("MBus7");
        if (string.IsNullOrWhiteSpace(inputSignal) || string.IsNullOrWhiteSpace(outputSignal))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "MBus3 oder MBus7 ist nicht zugeordnet.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-Gerätesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }

        var sourceVoltage = ParseEngineeringValue(parameters.StimulusChannel1?.Voltage);
        var lowerLimit = ParseEngineeringValue(parameters.AcquisitionChannel2?.LowerVoltageLimit);
        var upperLimit = ParseEngineeringValue(parameters.AcquisitionChannel2?.UpperVoltageLimit);
        if (sourceVoltage.HasValue)
        {
            WriteSignal(inputSignal!, sourceVoltage.Value);
            _observer.OnMessage($"{inputSignal} <= {sourceVoltage.Value.ToString("0.###", CultureInfo.InvariantCulture)} V");
        }

        var measuredValue = TryReadSignal(outputSignal!, out var measured, out var details)
            ? _evaluator.ToDouble(measured)
            : null;
        var outcome = EvaluateNumericOutcome(measuredValue, lowerLimit, upperLimit);
        var label = test.Parameters?.Name ?? test.Name ?? test.Id ?? "CDMA";
        var message = $"{label}: {measuredValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a"} V (limits {FormatLimit(lowerLimit)} .. {FormatLimit(upperLimit)}) => {outcome.ToString().ToUpperInvariant()}";
        _observer.OnMessage(message);
        if (!string.IsNullOrWhiteSpace(details))
        {
            _observer.OnMessage(details);
        }

        _observer.OnStepEvaluated(test, new StepEvaluation(label, outcome, measuredValue, lowerLimit, upperLimit, "V", details));
        return outcome;
    }

    private static bool IsDisabled(string? flag) => string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);

    private static double? ParseNullable(string? text)
    {
        return ParseEngineeringValue(text);
    }

    private static IReadOnlyList<int> ParseResultIndexes(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            return Array.Empty<int>();
        }

        var trimmed = specification.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        var indices = new List<int>();
        foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    private object? ResolveMeasurementInput(Test test, Record record)
    {
        if (ShouldSkipMeasurementPrompt(test))
        {
            return null;
        }

        var expression = record.Expression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        if (!VariableAddress.TryParse(expression, out var address))
        {
            return null;
        }

        var label = record.DrawingReference ?? record.Text ?? record.Id ?? test.Parameters?.Name ?? test.Name ?? "Measurement";
        label = string.IsNullOrWhiteSpace(label) ? "Measurement" : label.Trim();
        var unit = string.IsNullOrWhiteSpace(record.Unit) ? string.Empty : record.Unit!.Trim();
        if (!string.IsNullOrEmpty(unit))
        {
            label = $"{label} [{unit}]";
        }

        var prompt = $"{label}: ";
        string input;
        if (_interaction is IMeasurementInteractionProvider measurementProvider)
        {
            input = measurementProvider.PromptMeasurement(test, record, prompt, unit);
        }
        else
        {
            input = _interaction.PromptInput(prompt);
        }
        if (string.IsNullOrWhiteSpace(input))
        {
            return _context.GetValue(address);
        }

        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) &&
            !double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out numeric))
        {
            _context.SetValue(address, input);
            _observer.OnMessage($"{label} := {input}");
            return input;
        }

        _context.SetValue(address, numeric);
        _observer.OnMessage($"{label} := {numeric.ToString("0.###", CultureInfo.InvariantCulture)}");
        return numeric;
    }

    private static bool ShouldSkipMeasurementPrompt(Test? test)
    {
        if (test == null)
        {
            return false;
        }

        var id = (test.Id ?? string.Empty).Trim();
        if (id.StartsWith("ICT", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("SHRT", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("BCT", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(test.File))
        {
            var clean = test.File!.Trim().Trim('\'', '"');
            var ext = Path.GetExtension(clean);
            if (!string.IsNullOrWhiteSpace(ext) &&
                ext.Equals(".ctict", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureConditionContext(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return;
        }

        if (!_testerConfigurationAsked &&
            condition.Contains("Determined Tester Configuration", StringComparison.OrdinalIgnoreCase))
        {
            _testerConfigurationAsked = true;
            var input = _interaction.PromptInput("Enter number of AM2 modules installed (default 0): ");
            if (!int.TryParse(input, out var modules))
            {
                modules = 0;
            }

            _context.SetValue("'Determined Tester Configuration'\\Modules\\AM2\\Number", modules);
            _context.SetValue("\\'Determined Tester Configuration'\\Modules\\AM2\\Number", modules);
            _context.SetValue("\\\\'Determined Tester Configuration'\\Modules\\AM2\\Number", modules);
        }
    }

    private void CheckCancellation()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void ReportWireViz(string? candidate)
    {
        if (_wireVizResolver == null || string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (!_wireVizResolver.TryResolve(candidate, _signalState, out var resolutions) &&
            !_wireVizResolver.TryResolve(candidate, out resolutions))
        {
            return;
        }

        foreach (var resolution in resolutions)
        {
            _observer.OnMessage($"WireViz: {resolution.ToDisplayText()}");
        }
    }

    private void TryWriteExternalSignal(string? destination, object? value)
    {
        if (_externalDeviceSession == null || _wireVizResolver == null)
        {
            return;
        }

        var signalName = ExtractSignalName(destination);
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return;
        }

        RememberSignal(signalName!, value);
        if (!_wireVizResolver.TryResolve(signalName, _signalState, out var resolutions) &&
            !_wireVizResolver.TryResolve(signalName, out resolutions))
        {
            return;
        }

        if (_externalDeviceSession.TryWriteSignal(resolutions, value, _cancellationToken, out var writtenSignals, out var error))
        {
            _observer.OnMessage($"Python device input <= {string.Join(", ", writtenSignals)} = {_evaluator.ToText(value)}");
        }
        else if (!string.IsNullOrWhiteSpace(error))
        {
            _observer.OnMessage($"Python device input error for {signalName}: {error}");
        }
    }

    private object? ResolveExternalMeasurement(Record record)
    {
        if (_externalDeviceSession == null || _wireVizResolver == null)
        {
            return null;
        }

        foreach (var candidate in EnumerateSignalCandidates(record))
        {
            if (!_wireVizResolver.TryResolve(candidate, _signalState, out var resolutions) &&
                !_wireVizResolver.TryResolve(candidate, out resolutions))
            {
                continue;
            }

            if (_externalDeviceSession.TryReadSignal(resolutions, _cancellationToken, out var signalName, out var value, out var stateSummary, out var error))
            {
                _observer.OnMessage($"Python device read => {signalName} = {_evaluator.ToText(value)} @ {_externalDeviceSession.CurrentSimTimeMs} ms");
                if (!string.IsNullOrWhiteSpace(stateSummary))
                {
                    _observer.OnMessage($"Python state: {stateSummary}");
                }

                return value;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                _observer.OnMessage($"Python device read error for {candidate}: {error}");
                return null;
            }
        }

        return null;
    }

    private void ResetSimulationState()
    {
        _measurementBusSignals.Clear();
        _signalState.Clear();
    }

    private void PublishStepEvaluation(Test test, TestOutcome outcome, double? measured = null, double? lower = null, double? upper = null, string? unit = null, string? details = null)
    {
        var stepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        _observer.OnStepEvaluated(test, new StepEvaluation(stepName, outcome, measured, lower, upper, unit, details));
    }

    private string? ResolveMeasurementBusSignal(string busName)
    {
        return _measurementBusSignals.TryGetValue(busName, out var signal) ? signal : null;
    }

    private void WriteSignal(string signalName, object? value)
    {
        var normalized = ExtractSignalName(signalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        RememberSignal(normalized!, value);
        TryWriteExternalSignal(normalized, value);
    }

    private bool TryReadSignal(string signalName, out object? value, out string? details)
    {
        details = null;
        var normalized = ExtractSignalName(signalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return false;
        }

        if (_externalDeviceSession != null && _wireVizResolver != null &&
            (_wireVizResolver.TryResolve(normalized, _signalState, out var resolutions) || _wireVizResolver.TryResolve(normalized, out resolutions)))
        {
            if (_externalDeviceSession.TryReadSignal(resolutions, _cancellationToken, out var resolvedSignal, out value, out var stateSummary, out var error))
            {
                details = string.IsNullOrWhiteSpace(stateSummary) ? $"Quelle: {resolvedSignal}" : $"Quelle: {resolvedSignal} | state {stateSummary}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                details = error;
            }
        }

        value = null;
        return false;
    }

    private static TestOutcome EvaluateNumericOutcome(double? measured, double? lowerLimit, double? upperLimit)
    {
        if (!measured.HasValue)
        {
            return TestOutcome.Error;
        }

        if (lowerLimit.HasValue && measured.Value < lowerLimit.Value)
        {
            return TestOutcome.Fail;
        }

        if (upperLimit.HasValue && measured.Value > upperLimit.Value)
        {
            return TestOutcome.Fail;
        }

        return TestOutcome.Pass;
    }

    private static string FormatLimit(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
    }

    private static double? ParseEngineeringValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^\s*([-+]?\d+(?:[.,]\d+)?)\s*([A-Za-zµ]*)");
        if (!match.Success)
        {
            return null;
        }

        var numericText = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var unit = match.Groups[2].Value.Trim();
        return unit.ToLowerInvariant() switch
        {
            "" => value,
            "v" => value,
            "mv" => value / 1000d,
            "a" => value,
            "ma" => value / 1000d,
            "s" => value,
            "ms" => value / 1000d,
            _ => value
        };
    }

    private IEnumerable<string> EnumerateSignalCandidates(Record record)
    {
        var candidates = new[]
        {
            record.DrawingReference,
            record.TestPoint,
            record.Text,
            record.Expression
        };

        foreach (var candidate in candidates)
        {
            var normalized = ExtractSignalName(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
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

    private ExternalDeviceSession? CreateExternalDeviceSession()
    {
        var pipeName = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE");
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return null;
        }

        return new ExternalDeviceSession(new PythonDeviceSimulatorClient(pipeName));
    }

    private void TryAnnounceExternalDevice()
    {
        try
        {
            var response = _externalDeviceSession!.Hello(_cancellationToken);
            if (response.Ok)
            {
                _observer.OnMessage($"Python device connected @ {response.SimTimeMs ?? 0} ms");
            }
            else
            {
                _observer.OnMessage($"Python device hello failed: {response.ErrorCode}: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _observer.OnMessage($"Python device unavailable: {ex.Message}");
            _externalDeviceSession?.Dispose();
            _externalDeviceSession = null;
        }
    }

    private void TrySetExternalSignal(string signalName, object? value)
    {
        RememberSignal(signalName, value);
        if (_externalDeviceSession == null)
        {
            return;
        }

        if (_externalDeviceSession.TryWriteSignal(signalName, value, _cancellationToken, out var error))
        {
            _observer.OnMessage($"Python device input <= {signalName} = {_evaluator.ToText(value)}");
        }
        else if (!string.IsNullOrWhiteSpace(error))
        {
            _observer.OnMessage($"Python device input error for {signalName}: {error}");
        }
    }

    private void RememberSignal(string signalName, object? value)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return;
        }

        _signalState[signalName.Trim()] = value;
    }
}
