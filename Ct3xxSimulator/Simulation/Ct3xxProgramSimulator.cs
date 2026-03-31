using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.Programs;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulator.Simulation.Devices;
using Ct3xxSimulator.Simulation.FaultInjection;
using Ct3xxSimulator.Simulation.WireViz;
using Ct3xxSimulator.Simulation.Waveforms;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx programs against the simulator runtime, external device model and wiring model.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private readonly SimulationContext _context = new();
    private readonly ExpressionEvaluator _evaluator;
    private readonly IInteractionProvider _interaction;
    private readonly ISimulationObserver _observer;
    private readonly ISimulationExecutionController _executionController;
    private ExternalDeviceSession? _externalDeviceSession;
    private WireVizHarnessResolver? _wireVizResolver;
    private Ct3xxProgramFileSet? _fileSet;
    private Ct3xxProgram? _program;
    private SimulationFaultSet _faults = SimulationFaultSet.Empty;
    private IReadOnlyList<ResistorElementDefinition> _deviceCtctResistors = Array.Empty<ResistorElementDefinition>();
    private bool _testerConfigurationAsked;
    private CancellationToken _cancellationToken;
    private readonly Dictionary<string, string> _measurementBusSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _signalState = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _signalChangedAtMs = new(StringComparer.OrdinalIgnoreCase);
    private List<MeasurementCurvePoint> _currentCurvePoints = new();
    private ExternalDeviceStateSnapshot _externalDeviceState = ExternalDeviceStateSnapshot.Empty;
    private string? _currentStepName;
    private long _simulatedTimeMs;
    private string? _activeConcurrentGroupName;
    private string? _currentConcurrentEvent;
    private readonly List<ConcurrentTestHandle> _activeConcurrentTests = new();
    private readonly List<ConcurrentBranchRuntimeState> _concurrentBranchStates = new();
    private int? _activeConcurrentBranchIndex;
    private ActiveWaveformSession? _activeWaveformSession;
    private readonly List<Group> _groupStack = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Ct3xxProgramSimulator"/> class.
    /// </summary>
    /// <param name="interactionProvider">The optional interaction provider used for operator prompts.</param>
    /// <param name="observer">The optional observer that receives lifecycle and state notifications.</param>
    /// <param name="executionController">The optional execution controller used for step and snapshot pauses.</param>
    public Ct3xxProgramSimulator(
        IInteractionProvider? interactionProvider = null,
        ISimulationObserver? observer = null,
        ISimulationExecutionController? executionController = null)
    {
        _interaction = interactionProvider ?? new ConsoleInteractionProvider();
        _observer = observer ?? new ConsoleSimulationObserver();
        _executionController = executionController ?? new NullSimulationExecutionController();
        _evaluator = new ExpressionEvaluator(_context);
    }

    /// <summary>
    /// Executes an already parsed CT3xx program without an attached file set.
    /// </summary>
    /// <param name="program">The parsed CT3xx program to execute.</param>
    /// <param name="dutLoopIterations">The number of DUT loop iterations to execute.</param>
    /// <param name="cancellationToken">A token that cancels the simulation.</param>
    public void Run(Ct3xxProgram program, int dutLoopIterations, CancellationToken cancellationToken = default)
    {
        _externalDeviceSession?.Dispose();
        _externalDeviceSession = null;
        _wireVizResolver = null;
        _fileSet = null;
        _program = program;
        _faults = SimulationFaultSet.Empty;
        _deviceCtctResistors = Array.Empty<ResistorElementDefinition>();
        _context.SetProgramContext(null);
        ResetSimulationState();
        RunCore(program, dutLoopIterations, cancellationToken);
    }

    /// <summary>
    /// Executes a loaded CT3xx file set including signal tables, documents and program metadata.
    /// </summary>
    /// <param name="fileSet">The loaded CT3xx file set to execute.</param>
    /// <param name="dutLoopIterations">The number of DUT loop iterations to execute.</param>
    /// <param name="cancellationToken">A token that cancels the simulation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileSet"/> is <see langword="null"/>.</exception>
    public void Run(Ct3xxProgramFileSet fileSet, int dutLoopIterations, CancellationToken cancellationToken = default)
    {
        if (fileSet == null)
        {
            throw new ArgumentNullException(nameof(fileSet));
        }

        _externalDeviceSession?.Dispose();
        _externalDeviceSession = CreateExternalDeviceSession();
        _deviceCtctResistors = Array.Empty<ResistorElementDefinition>();
        _wireVizResolver = WireVizHarnessResolver.Create(fileSet, _deviceCtctResistors);
        _fileSet = fileSet;
        _program = fileSet.Program;
        _faults = SimulationFaultSet.Load(Environment.GetEnvironmentVariable("CT3XX_SIMULATION_MODEL_ROOT") ?? fileSet.ProgramDirectory);
        _context.SetProgramContext(fileSet.ProgramPath);
        ResetSimulationState();
        RunCore(fileSet.Program, dutLoopIterations, cancellationToken);
    }

    /// <summary>
    /// Executes RunCore.
    /// </summary>
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

        if (_faults.HasFaults)
        {
            _observer.OnMessage($"Faults loaded: {string.Join(", ", _faults.DescribeActiveFaults())}");
        }

        try
        {
            _context.ApplyTables(program.Tables, _evaluator);
        }
        catch (UndefinedVariableException ex)
        {
            _observer.OnMessage($"Variablenfehler in Programmtabellen: {ex.Message}");
            _context.MarkOutcome(TestOutcome.Error);
        }

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
                    try
                    {
                        _context.ApplyTable(table, _evaluator);
                    }
                    catch (UndefinedVariableException ex)
                    {
                        _observer.OnMessage($"Variablenfehler in Tabelle {table.Id}: {ex.Message}");
                        _context.MarkOutcome(TestOutcome.Error);
                    }
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

    /// <summary>
    /// Executes ExecuteGroup.
    /// </summary>
    private void ExecuteGroup(Group group)
    {
        if (IsDisabled(group.Disabled))
        {
            return;
        }

        try
        {
            EnsureConditionContext(group.ExecCondition);
            if (!_evaluator.EvaluateCondition(group.ExecCondition))
            {
                _observer.OnGroupSkipped(group, $"condition '{group.ExecCondition}' is FALSE");
                return;
            }
        }
        catch (UndefinedVariableException ex)
        {
            _observer.OnMessage($"Gruppe '{group.Name ?? group.Id}' uebersprungen: {ex.Message}");
            _context.MarkOutcome(TestOutcome.Error);
            return;
        }

        _observer.OnGroupStarted(group);
        _groupStack.Add(group);
        using var scope = _context.PushScope();
        var repeatEnabled = !string.IsNullOrWhiteSpace(group.RepeatCondition);
        var iteration = 0;
        var previousConcurrentGroupName = _activeConcurrentGroupName;
        do
        {
            CheckCancellation();
            iteration++;
            _context.SetValue("$LoopCounter", iteration);
            if (string.Equals(group.ExecMode, "concurrent", StringComparison.OrdinalIgnoreCase))
            {
                _activeConcurrentGroupName = group.Name ?? group.Id ?? "concurrent";
                ExecuteConcurrentGroupItems(group.Items);
                _activeConcurrentGroupName = previousConcurrentGroupName;
            }
            else
            {
                ExecuteSequenceItems(group.Items);
            }
        }
        while (repeatEnabled && iteration < 10 && SafeEvaluateRepeatCondition(group.RepeatCondition));

        if (repeatEnabled && iteration >= 10 && SafeEvaluateRepeatCondition(group.RepeatCondition))
        {
            _observer.OnMessage("Repeat condition still TRUE after 10 iterations, aborting group.");
        }

        _groupStack.RemoveAt(_groupStack.Count - 1);
        _observer.OnGroupCompleted(group);
        _executionController.WaitAfterGroup(group, _cancellationToken);
    }

    /// <summary>
    /// Executes ExecuteSequenceItems.
    /// </summary>
    private void ExecuteSequenceItems(IEnumerable<SequenceNode> items)
    {
        foreach (var item in items)
        {
            CheckCancellation();
            switch (item)
            {
                case Table table:
                    try
                    {
                        _context.ApplyTable(table, _evaluator);
                    }
                    catch (UndefinedVariableException ex)
                    {
                        _observer.OnMessage($"Variablenfehler in Tabelle {table.Id}: {ex.Message}");
                        _context.MarkOutcome(TestOutcome.Error);
                    }
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

    private bool SafeEvaluateRepeatCondition(string? condition)
    {
        try
        {
            return _evaluator.EvaluateCondition(condition);
        }
        catch (UndefinedVariableException ex)
        {
            _observer.OnMessage($"Repeat-Condition Fehler: {ex.Message}");
            _context.MarkOutcome(TestOutcome.Error);
            return false;
        }
    }

    /// <summary>
    /// Executes ExecuteConcurrentGroupItems.
    /// </summary>
    private void ExecuteConcurrentGroupItems(IEnumerable<SequenceNode> items)
    {
        RunConcurrentGroupScheduler(items.ToList());
    }

    /// <summary>
    /// Executes ExecuteDutLoop.
    /// </summary>
    private void ExecuteDutLoop(DutLoop loop, int iterations)
    {
        if (IsDisabled(loop.Disabled))
        {
            return;
        }

        try
        {
            EnsureConditionContext(loop.ExecCondition);
            if (!_evaluator.EvaluateCondition(loop.ExecCondition))
            {
                _observer.OnMessage("DUT loop skipped because condition is FALSE");
                return;
            }
        }
        catch (UndefinedVariableException ex)
        {
            _observer.OnMessage($"DUT loop skipped: {ex.Message}");
            _context.MarkOutcome(TestOutcome.Error);
            return;
        }

        using var scope = _context.PushScope();
        var runs = iterations <= 0 ? 1 : iterations;
        for (var run = 1; run <= runs; run++)
        {
            CheckCancellation();
            _context.SetValue("$LoopCounter", run);
            _observer.OnLoopIteration(run, runs);
            foreach (var item in loop.Items)
            {
                CheckCancellation();
                switch (item)
                {
                    case Table table:
                        try
                        {
                            _context.ApplyTable(table, _evaluator);
                        }
                        catch (UndefinedVariableException ex)
                        {
                            _observer.OnMessage($"Variablenfehler in Tabelle {table.Id}: {ex.Message}");
                            _context.MarkOutcome(TestOutcome.Error);
                        }
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

    /// <summary>
    /// Executes ExecuteTest.
    /// </summary>
    private void ExecuteTest(Test test)
    {
        ExecuteTestCore(test, advanceStepDuration: true);
    }

    /// <summary>
    /// Executes ExecuteTestWithoutStepDuration.
    /// </summary>
    private void ExecuteTestWithoutStepDuration(Test test)
    {
        ExecuteTestCore(test, advanceStepDuration: false);
    }

    /// <summary>
    /// Executes ExecuteTestCore.
    /// </summary>
    private void ExecuteTestCore(Test test, bool advanceStepDuration)
    {
        if (IsDisabled(test.Disabled))
        {
            return;
        }

        if (advanceStepDuration)
        {
            AdvanceTime(GetStepDurationMs(test));
        }

        _currentCurvePoints = new List<MeasurementCurvePoint>();
        _executionController.WaitBeforeTest(test, _cancellationToken);
        _observer.OnTestStarted(test);
        _currentStepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        PublishStateSnapshot();

        TestOutcome outcome;
        try
        {
            outcome = test.Id?.ToUpperInvariant() switch
            {
                "GSD^" => RunAssignmentTest(test),
                "IOXX" => RunDigitalIoControlTest(test),
                "2C2I" => RunI2cInterfaceTest(test),
                "SPIX" => RunSpiIoControlTest(test),
                "SMUD" => RunSmudPowerSupplyTest(test),
                "CTCT" => RunConnectionContactTest(test),
                "ICT " => RunIctTest(test),
                "SHRT" => RunShortcutTest(test),
                "E488" => RunInterfaceTest(test),
                "PET$" => RunEvaluationTest(test),
                "PRT^" => RunOperatorTest(test),
                "ECLL" => RunExecutableCallTest(test),
                "PWT$" => RunWaitTest(test),
                "SC2C" => RunScannerConnectTest(test),
                "CDMA" => RunCdmaTest(test),
                "DM30" => RunDm30Test(test),
                "2ARB" => RunWaveformTest(test),
                "SA1T" => RunWaveformTest(test),
                "TRGA" => RunWaveformTest(test),
                "AM4N" => RunAm4nTest(test),
                "AM2A" => RunAm2aTest(test),
                "DNIS" => RunDnisTest(test),
                "BSC1" => RunBoundaryScanTest(test),
                "ODBC" => RunOdbcTest(test),
                "FCA^" => RunFunctionCallTest(test),
                _ => RunGenericTest(test)
            };
        }
        catch (UndefinedVariableException ex)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: ex.Message);
            outcome = TestOutcome.Error;
        }

        _context.MarkOutcome(outcome);
        _observer.OnTestCompleted(test, outcome);
        SampleActiveWaveformSignals();

        if (test.Items.Count > 0 && !HandlesOwnChildSequence(test))
        {
            ExecuteSequenceItems(test.Items);
        }

        _executionController.WaitAfterTest(test, _cancellationToken);
    }

    /// <summary>
    /// Executes RunAssignmentTest.
    /// </summary>
    private TestOutcome RunAssignmentTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Pass);
            return TestOutcome.Pass;
        }

        var assignments = new List<string>();
        var traces = new List<StepConnectionTrace>();
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
                var assignmentText = $"{address} := {_evaluator.ToText(value)}";
                assignments.Add(assignmentText);
                _observer.OnMessage(assignmentText);
                var signalName = ExtractSignalName(record.Destination);
                if (!string.IsNullOrWhiteSpace(signalName))
                {
                    if (_wireVizResolver == null ||
                        _wireVizResolver.TryResolve(signalName!, out _) ||
                        _wireVizResolver.TryResolveRuntimeTargets(signalName!, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, true, out _))
                    {
                        RememberSignal(signalName!, value);
                    }

                    TryWriteExternalSignal(record.Destination, value);
                    traces.AddRange(CollectSignalTraces(signalName!, "Ansteuerung"));
                }
            }
        }

        PublishStepEvaluation(
            test,
            TestOutcome.Pass,
            details: assignments.Count == 0 ? null : string.Join(", ", assignments),
            traces: traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList());
        return TestOutcome.Pass;
    }

    /// <summary>
    /// Executes RunEvaluationTest.
    /// </summary>
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
                var traces = CollectRecordTraces(record);
                var measured = _evaluator.ToDouble(value);
                RecordCurvePoint(label: record.DrawingReference ?? record.Expression ?? record.Id ?? "Messung", value: measured, unit: record.Unit);
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
                PublishStepEvaluation(
                    test,
                    pass ? TestOutcome.Pass : TestOutcome.Fail,
                    measured: measured,
                    lower: lower,
                    upper: upper,
                    unit: unit,
                    stepNameOverride: label,
                    traces: traces,
                    curvePoints: CaptureCurvePoints());
                if (!pass)
                {
                    overall = TestOutcome.Fail;
                }
            }
        }

        return overall;
    }

    /// <summary>
    /// Executes RunOperatorTest.
    /// </summary>
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

    /// <summary>
    /// Executes RunDigitalIoControlTest.
    /// </summary>
    private TestOutcome RunDigitalIoControlTest(Test test)
    {
        var parameters = test.Parameters;
        if ((parameters?.ExtraElements == null || parameters.ExtraElements.Length == 0) &&
            (parameters?.Records == null || parameters.Records.Count == 0))
        {
            PublishStepEvaluation(test, TestOutcome.Pass, details: "IOXX ohne Datensaetze.");
            return TestOutcome.Pass;
        }

        var actions = new List<string>();
        var traces = new List<StepConnectionTrace>();
        long maxWaitMs = 0;
        var ioActions = new List<(string ChannelName, string OutState, string? RelayState, long WaitMs, string? Direction)>();

        if (parameters?.ExtraElements != null)
        {
            foreach (var element in parameters.ExtraElements)
            {
                if (!string.Equals(element.Name, "Record", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var disabled = element.GetAttribute("Disabled");
                if (string.Equals(disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var channelName = element.GetAttribute("ChannelName");
                var outState = element.GetAttribute("Out");
                var relayState = element.GetAttribute("RelayState");
                var direction = element.GetAttribute("Direction");
                var waitMs = ParseDurationMilliseconds(element.GetAttribute("WaitTime"));
                if (!string.IsNullOrWhiteSpace(channelName) && !string.IsNullOrWhiteSpace(outState))
                {
                    ioActions.Add((channelName, outState, relayState, waitMs, direction));
                }
            }
        }

        if (parameters?.Records != null)
        {
            foreach (var record in parameters.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var channelName = GetRecordAttribute(record, "ChannelName") ?? record.Destination ?? record.Variable;
                var outState = GetRecordAttribute(record, "Out") ?? record.Expression ?? record.Text;
                var relayState = GetRecordAttribute(record, "RelayState");
                var direction = record.Direction ?? GetRecordAttribute(record, "Direction");
                var waitMs = ParseDurationMilliseconds(GetRecordAttribute(record, "WaitTime"));
                if (!string.IsNullOrWhiteSpace(channelName) && !string.IsNullOrWhiteSpace(outState))
                {
                    ioActions.Add((channelName, outState, relayState, waitMs, direction));
                }
            }
        }

        foreach (var action in ioActions)
        {
            if (!string.Equals(action.Direction, "Send", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var channelName = ExtractSignalName(action.ChannelName);
            if (string.IsNullOrWhiteSpace(channelName))
            {
                continue;
            }

            var outState = action.OutState.Trim().Trim('\'', '"');
            object? value = ResolveDigitalIoValue(channelName!, outState, out var valueDescription);

            WriteSignal(channelName!, value);
            traces.AddRange(CollectSignalTraces(channelName!, "Ansteuerung"));

            var relayState = action.RelayState?.Trim().Trim('\'', '"');
            maxWaitMs = Math.Max(maxWaitMs, action.WaitMs);

            actions.Add(string.IsNullOrWhiteSpace(valueDescription)
                ? $"{channelName}={_evaluator.ToText(value)} Relay={relayState}"
                : $"{channelName}={_evaluator.ToText(value)} Relay={relayState} ({valueDescription})");
        }

        if (maxWaitMs > 0)
        {
            AdvanceTime(maxWaitMs);
        }

        PublishStepEvaluation(
            test,
            TestOutcome.Pass,
            details: actions.Count == 0 ? "IOXX ohne wirksame Send-Datensaetze." : string.Join(", ", actions),
            traces: traces
                .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList());
        return TestOutcome.Pass;
    }

    /// <summary>
    /// Executes ResolveDigitalIoValue.
    /// </summary>
    private object? ResolveDigitalIoValue(string signalName, string outState, out string? description)
    {
        description = null;
        if (_wireVizResolver != null &&
            _wireVizResolver.TryResolveTesterOutputValue(signalName, outState, _signalState, out var configuredValue, out description))
        {
            return configuredValue;
        }

        return outState.ToUpperInvariant() switch
        {
            "H" => 24d,
            "L" => 0d,
            "1" => 24d,
            "0" => 0d,
            "TRUE" => true,
            "FALSE" => false,
            _ => true
        };
    }

    /// <summary>
    /// Executes RunGenericTest.
    /// </summary>
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

    /// <summary>
    /// Executes RunExecutableCallTest.
    /// </summary>
    private TestOutcome RunExecutableCallTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ECLL ohne Parameter.");
            return TestOutcome.Error;
        }

        var fileExpression = GetParameterAttribute(parameters, "ExeFileName");
        var executablePath = _evaluator.ResolveText(fileExpression);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "ECLL ohne ExeFileName.");
            return TestOutcome.Error;
        }

        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: $"Datei nicht gefunden: {executablePath}");
            return TestOutcome.Error;
        }

        var waitForFinish = ParseYesNo(GetParameterAttribute(parameters, "WaitForFinish"), defaultValue: true);
        var evaluateExitCode = ParseYesNo(GetParameterAttribute(parameters, "EvaluateExitCode"), defaultValue: false);
        if (evaluateExitCode && !waitForFinish)
        {
            waitForFinish = true;
            _observer.OnMessage("ECLL: WaitForFinish wurde aktiviert, weil EvaluateExitCode ausgewertet werden soll.");
        }

        var startInfo = BuildExecutableStartInfo(executablePath);
        _observer.OnMessage($"ECLL: starte {startInfo.FileName} {startInfo.Arguments}".Trim());

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        if (!waitForFinish)
        {
            PublishStepEvaluation(test, TestOutcome.Pass, details: $"Gestartet: {Path.GetFileName(executablePath)}");
            return TestOutcome.Pass;
        }

        process.WaitForExit();
        var exitCode = process.ExitCode;
        _observer.OnMessage($"ECLL: ExitCode={exitCode}");

        if (!evaluateExitCode)
        {
            PublishStepEvaluation(test, TestOutcome.Pass, measured: exitCode, details: $"ExitCode={exitCode}");
            return TestOutcome.Pass;
        }

        var expectedExitCode = ParseExpectedExitCode(parameters);
        var outcome = exitCode == expectedExitCode ? TestOutcome.Pass : TestOutcome.Fail;
        PublishStepEvaluation(test, outcome, measured: exitCode, lower: expectedExitCode, upper: expectedExitCode, details: $"ExitCode={exitCode}, erwartet={expectedExitCode}");
        return outcome;
    }

    /// <summary>
    /// Executes TryStartConcurrentTest.
    /// </summary>
    private bool TryStartConcurrentTest(Test test, int branchIndex, out ConcurrentTestHandle? handle)
    {
        handle = null;
        if (!string.Equals(test.Id, "ECLL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parameters = test.Parameters;
        if (parameters == null)
        {
            return false;
        }

        var waitForFinish = ParseYesNo(GetParameterAttribute(parameters, "WaitForFinish"), defaultValue: true);
        if (!waitForFinish)
        {
            return false;
        }

        var fileExpression = GetParameterAttribute(parameters, "ExeFileName");
        var executablePath = _evaluator.ResolveText(fileExpression);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
        {
            return false;
        }

        AdvanceTime(GetStepDurationMs(test));
        _currentCurvePoints = new List<MeasurementCurvePoint>();
        _executionController.WaitBeforeTest(test, _cancellationToken);
        _observer.OnTestStarted(test);
        _currentStepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        PublishStateSnapshot();

        var evaluateExitCode = ParseYesNo(GetParameterAttribute(parameters, "EvaluateExitCode"), defaultValue: false);
        var expectedExitCode = ParseExpectedExitCode(parameters);
        var startInfo = BuildExecutableStartInfo(executablePath);
        _observer.OnMessage($"ECLL: starte parallel {startInfo.FileName} {startInfo.Arguments}".Trim());
        UpdateConcurrentBranchState(branchIndex, "running", DescribeSequenceNode(test), details: $"Prozess gestartet: {Path.GetFileName(executablePath)}");

        var process = new Process { StartInfo = startInfo };
        process.Start();

        handle = new ConcurrentTestHandle(
            branchIndex,
            test,
            process,
            executablePath,
            evaluateExitCode,
            expectedExitCode);
        return true;
    }

    /// <summary>
    /// Executes CompleteConcurrentTest.
    /// </summary>
    private void CompleteConcurrentTest(ConcurrentTestHandle handle)
    {
        using var process = handle.Process;
        process.WaitForExit();
        var exitCode = process.ExitCode;
        _currentConcurrentEvent = $"process_exit:{handle.Test.Parameters?.Name ?? handle.Test.Name ?? handle.Test.Id ?? "Test"}";
        UpdateConcurrentBranchState(handle.BranchIndex, "running", DescribeSequenceNode(handle.Test), details: $"ExitCode={exitCode}");
        PublishStateSnapshot();
        _observer.OnMessage($"ECLL: ExitCode={exitCode}");

        TestOutcome outcome;
        if (!handle.EvaluateExitCode)
        {
            outcome = TestOutcome.Pass;
            PublishStepEvaluation(handle.Test, TestOutcome.Pass, measured: exitCode, details: $"ExitCode={exitCode}");
        }
        else
        {
            outcome = exitCode == handle.ExpectedExitCode ? TestOutcome.Pass : TestOutcome.Fail;
            PublishStepEvaluation(
                handle.Test,
                outcome,
                measured: exitCode,
                lower: handle.ExpectedExitCode,
                upper: handle.ExpectedExitCode,
                details: $"ExitCode={exitCode}, erwartet={handle.ExpectedExitCode}");
        }

        _context.MarkOutcome(outcome);
        _observer.OnTestCompleted(handle.Test, outcome);
        _executionController.WaitAfterTest(handle.Test, _cancellationToken);
        _currentConcurrentEvent = null;
    }

    /// <summary>
    /// Executes InitializeConcurrentBranchStates.
    /// </summary>
    private void InitializeConcurrentBranchStates(IReadOnlyList<SequenceNode> items)
    {
        _concurrentBranchStates.Clear();
        for (var index = 0; index < items.Count; index++)
        {
            _concurrentBranchStates.Add(new ConcurrentBranchRuntimeState(
                index,
                GetBranchName(items[index], index),
                DescribeSequenceNode(items[index]),
                "queued"));
        }
    }

    /// <summary>
    /// Executes UpdateConcurrentBranchState.
    /// </summary>
    private void UpdateConcurrentBranchState(int branchIndex, string status, string? currentItem = null, long? waitUntilTimeMs = null, string? details = null)
    {
        if (branchIndex < 0 || branchIndex >= _concurrentBranchStates.Count)
        {
            return;
        }

        var existing = _concurrentBranchStates[branchIndex];
        _concurrentBranchStates[branchIndex] = existing with
        {
            Status = status,
            CurrentItem = currentItem ?? existing.CurrentItem,
            WaitUntilTimeMs = waitUntilTimeMs,
            Details = details
        };
    }

    /// <summary>
    /// Executes CompleteConcurrentBranchState.
    /// </summary>
    private void CompleteConcurrentBranchState(int branchIndex)
    {
        UpdateConcurrentBranchState(branchIndex, "completed", details: null, waitUntilTimeMs: null);
    }

    /// <summary>
    /// Executes BeginConcurrentBranchScope.
    /// </summary>
    private IDisposable BeginConcurrentBranchScope(int branchIndex)
    {
        var previous = _activeConcurrentBranchIndex;
        _activeConcurrentBranchIndex = branchIndex;
        return new ScopeGuard(() => _activeConcurrentBranchIndex = previous);
    }

    /// <summary>
    /// Executes GetBranchName.
    /// </summary>
    private static string GetBranchName(SequenceNode item, int index)
    {
        return item switch
        {
            Test test => test.Parameters?.Name ?? test.Name ?? test.Id ?? $"Branch {index + 1}",
            Group group => group.Name ?? group.Id ?? $"Branch {index + 1}",
            Table table => table.Id ?? $"Table {index + 1}",
            _ => $"Branch {index + 1}"
        };
    }

    /// <summary>
    /// Executes DescribeSequenceNode.
    /// </summary>
    private static string DescribeSequenceNode(SequenceNode item)
    {
        return item switch
        {
            Test test => $"{test.Id ?? "Test"}: {test.Parameters?.Name ?? test.Name ?? "Unnamed"}",
            Group group => $"{group.Id ?? "Group"}: {group.Name ?? "Unnamed"}",
            Table table => $"{table.Id ?? "Table"}",
            _ => item.GetType().Name
        };
    }

    /// <summary>
    /// Executes RunWaitTest.
    /// </summary>
    private TestOutcome RunWaitTest(Test test)
    {
        var waitExpression = GetParameterAttribute(test.Parameters, "WaitTime");
        var delayMs = ParseDurationMilliseconds(waitExpression);
        if (delayMs > 0)
        {
            if (_activeConcurrentBranchIndex.HasValue)
            {
                var branchName = _concurrentBranchStates[_activeConcurrentBranchIndex.Value].BranchName;
                UpdateConcurrentBranchState(_activeConcurrentBranchIndex.Value, "waiting", DescribeSequenceNode(test), _simulatedTimeMs + delayMs, $"WaitTime={delayMs} ms");
                _currentConcurrentEvent = $"branch_waiting:{branchName}";
                PublishStateSnapshot();
            }
            else
            {
                Thread.Sleep((int)Math.Min(delayMs, 5_000));
            }

            AdvanceTime(delayMs);

            if (_activeConcurrentBranchIndex.HasValue)
            {
                var branchName = _concurrentBranchStates[_activeConcurrentBranchIndex.Value].BranchName;
                UpdateConcurrentBranchState(_activeConcurrentBranchIndex.Value, "running", DescribeSequenceNode(test), null, $"Wait abgeschlossen ({delayMs} ms)");
                _currentConcurrentEvent = $"branch_resumed:{branchName}";
                PublishStateSnapshot();
                _currentConcurrentEvent = null;
            }
        }

        PublishStepEvaluation(test, TestOutcome.Pass, details: $"WaitTime={delayMs} ms");
        return TestOutcome.Pass;
    }

    // E488 is a communication test for RS232 / IEEE-488 / VISA style interfaces.
    // The simulator sends commands to the external DUT, reads the response and maps it
    // back into CT3xx variables so that following evaluation steps can use it.
    /// <summary>
    /// Executes RunInterfaceTest.
    /// </summary>
    private TestOutcome RunInterfaceTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "E488 ohne Parameter.");
            return TestOutcome.Error;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "E488 ohne aktive GerÃ¤tesimulation.");
            return TestOutcome.Error;
        }

        var operations = new List<string>();
        var overallOutcome = TestOutcome.Pass;

        foreach (var table in parameters.Tables)
        {
            foreach (var record in table.Records)
            {
                if (string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var interfaceName = record.Device ?? GetRecordAttribute(record, "Device");
                if (string.IsNullOrWhiteSpace(interfaceName))
                {
                    continue;
                }

                var direction = NormalizeInterfaceDirection(record.Direction ?? GetRecordAttribute(record, "Direction"));
                var command = _evaluator.ResolveText(record.Command ?? GetRecordAttribute(record, "Command") ?? record.Expression ?? record.Text);
                object? responsePayload = null;
                string? error = null;
                var operationOk = true;

                if (direction.send)
                {
                    if (_activeConcurrentBranchIndex.HasValue)
                    {
                        var branchName = _concurrentBranchStates[_activeConcurrentBranchIndex.Value].BranchName;
                        UpdateConcurrentBranchState(_activeConcurrentBranchIndex.Value, "running", DescribeSequenceNode(test), details: $"Schnittstellenkommando {interfaceName}");
                        _currentConcurrentEvent = $"interface_request:{branchName}";
                        PublishStateSnapshot();
                    }

                    operationOk = _externalDeviceSession.TrySendInterface(interfaceName!, command, _cancellationToken, out responsePayload, out error, _simulatedTimeMs);
                    _observer.OnMessage($"E488 Schnittstelle SEND {interfaceName}: {command}");
                }
                else if (direction.receive)
                {
                    if (_activeConcurrentBranchIndex.HasValue)
                    {
                        var branchName = _concurrentBranchStates[_activeConcurrentBranchIndex.Value].BranchName;
                        UpdateConcurrentBranchState(_activeConcurrentBranchIndex.Value, "running", DescribeSequenceNode(test), details: $"Schnittstellenlesezyklus {interfaceName}");
                        _currentConcurrentEvent = $"interface_read:{branchName}";
                        PublishStateSnapshot();
                    }

                    operationOk = _externalDeviceSession.TryReadInterface(interfaceName!, _cancellationToken, out responsePayload, out error, _simulatedTimeMs);
                    _observer.OnMessage($"E488 Schnittstelle READ {interfaceName}");
                }

                if (!operationOk)
                {
                    overallOutcome = TestOutcome.Error;
                    operations.Add($"{interfaceName}: {error}");
                    continue;
                }

                RefreshExternalDeviceState();
                AssignInterfaceResponse(record, responsePayload);
                var responseText = DescribeInterfaceResponse(responsePayload);
                operations.Add(direction.receive || direction.send ? $"{interfaceName}: {responseText}" : interfaceName!);
                _observer.OnMessage($"E488 Schnittstelle RECV {interfaceName}: {responseText}");
                if (_activeConcurrentBranchIndex.HasValue)
                {
                    var branchName = _concurrentBranchStates[_activeConcurrentBranchIndex.Value].BranchName;
                    UpdateConcurrentBranchState(_activeConcurrentBranchIndex.Value, "running", DescribeSequenceNode(test), details: $"Antwort {responseText}");
                    _currentConcurrentEvent = $"interface_response:{branchName}";
                    PublishStateSnapshot();
                    _currentConcurrentEvent = null;
                }
            }
        }

        PublishStepEvaluation(
            test,
            overallOutcome,
            details: operations.Count == 0 ? "E488 ohne wirksame DatensÃ¤tze." : string.Join(", ", operations));
        return overallOutcome;
    }

    /// <summary>
    /// Executes RunWaveformTest.
    /// </summary>
    private TestOutcome RunWaveformTest(Test test)
    {
        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "Waveform-Test ohne aktive GerÃ¤tesimulation.");
            return TestOutcome.Error;
        }

        if (!ArbitraryWaveformLoader.TryLoadAll(_fileSet, test, out var waveforms, out var error))
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: error);
            return TestOutcome.Error;
        }

        var channelRuns = new List<(AppliedWaveform Waveform, string StimulusSignal, string? ObserveSignal, string ExternalStimulusSignal, string? ExternalObserveSignal, JsonObject? Response)>();
        var traces = new List<StepConnectionTrace>();

        foreach (var waveform in waveforms)
        {
            var stimulusSignal = ResolveWaveformRuntimeSignal(waveform.SignalName, waveform.Metadata, "MBUS_ARB", "TP_ARB");
            var observeSignal = ResolveWaveformRuntimeSignal(waveform.SignalName, waveform.Metadata, "MBUS_SCO", "TP_SCO");
            var externalStimulusSignal = ResolveExternalWaveformTarget(stimulusSignal, true);
            var externalObserveSignal = string.IsNullOrWhiteSpace(observeSignal) ? null : ResolveExternalWaveformTarget(observeSignal!, false);
            var observeSignals = new List<string>();
            if (!string.IsNullOrWhiteSpace(externalObserveSignal))
            {
                observeSignals.Add(externalObserveSignal!);
            }

            var options = new
            {
                observe_signals = observeSignals,
                capture_signals = observeSignals,
                capture_sample_count = Math.Min(Math.Max(waveform.Points.Count, 16), 128),
                capture_duration_ms = waveform.DurationMs
            };

            var externalWaveform = new AppliedWaveform(externalStimulusSignal, waveform.WaveformName, waveform.Points, waveform.SampleTimeMs, waveform.DelayMs, waveform.Periodic, waveform.Cycles, waveform.Metadata);
            if (!_externalDeviceSession.TrySetWaveform(externalStimulusSignal, externalWaveform, options, _cancellationToken, out var response, out error, _simulatedTimeMs))
            {
                PublishStepEvaluation(test, TestOutcome.Error, details: $"Waveform konnte nicht gesetzt werden: {error}");
                return TestOutcome.Error;
            }

            LogWaveformResponse(test, response);
            channelRuns.Add((waveform, stimulusSignal, observeSignal, externalStimulusSignal, externalObserveSignal, response));
            traces.AddRange(CollectSignalTraces(stimulusSignal, "Waveform Stimulus"));
            if (!string.IsNullOrWhiteSpace(observeSignal))
            {
                traces.AddRange(CollectSignalTraces(observeSignal!, "Waveform Response"));
            }
        }

        StartActiveWaveformSession(channelRuns);
        ActiveWaveformSession? completedWaveformSession = null;
        try
        {
            SampleActiveWaveformSignals();
            if (test.Items.Count > 0)
            {
                ExecuteSequenceItems(test.Items);
            }

            SampleActiveWaveformSignals();
        }
        finally
        {
            completedWaveformSession = _activeWaveformSession;
            EndActiveWaveformSession();
        }

        foreach (var channel in channelRuns)
        {
            if (!string.IsNullOrWhiteSpace(channel.ExternalObserveSignal) &&
                _externalDeviceSession.TryReadWaveform(channel.ExternalObserveSignal!, new { }, _cancellationToken, out var responseWaveform, out error, _simulatedTimeMs) &&
                responseWaveform?["result"] is JsonObject readResult)
            {
                _observer.OnMessage($"Waveform readback fuer {channel.ExternalObserveSignal}: {readResult.ToJsonString()}");
            }
        }

        var primaryChannel = channelRuns[0];
        TryPublishWaveformVariables(primaryChannel.StimulusSignal, primaryChannel.ObserveSignal, primaryChannel.Response);
        var waveformOutcome = EvaluateWaveformOutcome(completedWaveformSession, channelRuns, out var waveformDetails, out var lastObserved);
        var waveformCurvePoints = BuildWaveformCurvePoints(completedWaveformSession);
        PublishStepEvaluation(
            test,
            waveformOutcome,
            measured: lastObserved,
            unit: "V",
            details: string.Join(" | ", new[]
            {
                string.Join(", ", channelRuns.Select(item => $"{item.Waveform.WaveformName} -> {item.StimulusSignal}")),
                waveformDetails
            }.Where(item => !string.IsNullOrWhiteSpace(item))),
            traces: traces,
            curvePoints: waveformCurvePoints.Count == 0 ? null : waveformCurvePoints);
        EndActiveWaveformSession();
        return waveformOutcome;
    }

    /// <summary>
    /// Executes RunScannerConnectTest.
    /// </summary>
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
        WriteSignal("VCC_Plus", 24d);
        TrySetExternalSignal("HELPER_SUPPLY", true);
        PublishStepEvaluation(test, TestOutcome.Pass, details: details);
        return TestOutcome.Pass;
    }

    /// <summary>
    /// Executes RunCdmaTest.
    /// </summary>
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
            PublishStepEvaluation(test, TestOutcome.Error, details: "Python-GerÃ¤tesimulation ist nicht verbunden.");
            return TestOutcome.Error;
        }

        var sourceVoltage = ParseEngineeringValue(parameters.StimulusChannel1?.Voltage);
        var lowerLimit = ParseEngineeringValue(parameters.AcquisitionChannel2?.LowerVoltageLimit);
        var upperLimit = ParseEngineeringValue(parameters.AcquisitionChannel2?.UpperVoltageLimit);
        if (sourceVoltage.HasValue)
        {
            WriteSignal(inputSignal!, sourceVoltage.Value);
            RecordCurvePoint($"Stimulus {inputSignal}", sourceVoltage.Value, "V");
            _observer.OnMessage($"{inputSignal} <= {sourceVoltage.Value.ToString("0.###", CultureInfo.InvariantCulture)} V");
        }

        var traces = new List<StepConnectionTrace>();
        traces.AddRange(CollectSignalTraces(inputSignal!, "Ansteuerung"));
        traces.AddRange(CollectSignalTraces(outputSignal!, "Messpfad"));

        var measuredValue = TryReadSignal(outputSignal!, out var measured, out var details)
            ? _evaluator.ToDouble(measured)
            : null;
        RecordCurvePoint($"Messung {outputSignal}", measuredValue, "V");
        var outcome = EvaluateNumericOutcome(measuredValue, lowerLimit, upperLimit);
        var label = test.Parameters?.Name ?? test.Name ?? test.Id ?? "CDMA";
        var message = $"{label}: {measuredValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a"} V (limits {FormatLimit(lowerLimit)} .. {FormatLimit(upperLimit)}) => {outcome.ToString().ToUpperInvariant()}";
        _observer.OnMessage(message);
        if (!string.IsNullOrWhiteSpace(details))
        {
            _observer.OnMessage(details);
        }

        _observer.OnStepEvaluated(test, new StepEvaluation(label, outcome, measuredValue, lowerLimit, upperLimit, "V", details, traces, CaptureCurvePoints()));
        return outcome;
    }

    /// <summary>
    /// Executes IsDisabled.
    /// </summary>
    private static bool IsDisabled(string? flag) => string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes ParseNullable.
    /// </summary>
    private static double? ParseNullable(string? text)
    {
        return ParseEngineeringValue(text);
    }

    /// <summary>
    /// Executes ParseResultIndexes.
    /// </summary>
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

    /// <summary>
    /// Executes ResolveMeasurementInput.
    /// </summary>
    private object? ResolveMeasurementInput(Test test, Record record)
    {
        var expression = record.Expression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        if (!VariableAddress.TryParse(expression, out var address))
        {
            return null;
        }

        // Only explicit operator interaction tests should ask the user.
        // Evaluation tests read already available variables and signals.
        return _context.GetValue(address);
    }

    /// <summary>
    /// Executes EnsureConditionContext.
    /// </summary>
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

    /// <summary>
    /// Executes CheckCancellation.
    /// </summary>
    private void CheckCancellation()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Executes ReportWireViz.
    /// </summary>
    private void ReportWireViz(string? candidate)
    {
        if (_wireVizResolver == null || string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (!_wireVizResolver.TryResolve(candidate, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, out var resolutions) &&
            !_wireVizResolver.TryResolve(candidate, out resolutions))
        {
            return;
        }

        foreach (var resolution in resolutions)
        {
            _observer.OnMessage($"WireViz: {resolution.ToDisplayText()}");
        }
    }

    /// <summary>
    /// Executes TryWriteExternalSignal.
    /// </summary>
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
        if (!_wireVizResolver.TryResolveRuntimeTargets(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, true, out var runtimeTargets))
        {
            if (_externalDeviceSession.TryWriteSignal(signalName, value, _cancellationToken, out var fallbackError, _simulatedTimeMs))
            {
                RefreshExternalDeviceState();
                _observer.OnMessage($"Python device input <= {signalName} = {_evaluator.ToText(value)}");
            }
            else if (!string.IsNullOrWhiteSpace(fallbackError))
            {
                _observer.OnMessage($"Python device input error for {signalName}: {fallbackError}");
            }

            return;
        }

        if (_externalDeviceSession.TryWriteSignal(runtimeTargets, value, _cancellationToken, out var writtenSignals, out var error, _simulatedTimeMs))
        {
            RefreshExternalDeviceState();
            _observer.OnMessage($"Python device input <= {string.Join(", ", writtenSignals)} = {_evaluator.ToText(value)}");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _observer.OnMessage($"Python device input error for {signalName}: {error}");
            }

            if (_externalDeviceSession.TryWriteSignal(signalName, value, _cancellationToken, out var fallbackError, _simulatedTimeMs))
            {
                RefreshExternalDeviceState();
                _observer.OnMessage($"Python device input <= {signalName} = {_evaluator.ToText(value)}");
            }
            else if (!string.IsNullOrWhiteSpace(fallbackError))
            {
                _observer.OnMessage($"Python device input error for {signalName}: {fallbackError}");
            }
        }
    }

    /// <summary>
    /// Executes ResolveExternalMeasurement.
    /// </summary>
    private object? ResolveExternalMeasurement(Record record)
    {
        if (_externalDeviceSession == null || _wireVizResolver == null)
        {
            return null;
        }

        foreach (var candidate in EnumerateSignalCandidates(record))
        {
            var forcedValue = _faults.TryGetForcedSignal(candidate);
            if (forcedValue.HasValue)
            {
                var drifted = _faults.ApplySignalDrift(candidate, forcedValue.Value);
                _observer.OnMessage($"Fault value => {candidate} = {drifted.ToString("0.###", CultureInfo.InvariantCulture)}");
                return drifted;
            }

            if (!_wireVizResolver.TryResolveRuntimeTargets(candidate, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, false, out var runtimeTargets))
            {
                continue;
            }

            if (_externalDeviceSession.TryReadSignal(runtimeTargets, _cancellationToken, out var signalName, out var value, out var stateSummary, out var error, _simulatedTimeMs))
            {
                RefreshExternalDeviceState();
                _observer.OnMessage($"Python device read => {signalName} = {_evaluator.ToText(value)} @ {_externalDeviceSession.CurrentSimTimeMs} ms");
                if (!string.IsNullOrWhiteSpace(stateSummary))
                {
                    _observer.OnMessage($"Python state: {stateSummary}");
                }

                if (value is double numeric)
                {
                    return _faults.ApplySignalDrift(signalName, numeric);
                }

                if (value is float numericFloat)
                {
                    return _faults.ApplySignalDrift(signalName, numericFloat);
                }

                if (value is int numericInt)
                {
                    return _faults.ApplySignalDrift(signalName, numericInt);
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

    /// <summary>
    /// Executes ResetSimulationState.
    /// </summary>
    private void ResetSimulationState()
    {
        _measurementBusSignals.Clear();
        _signalState.Clear();
        _signalChangedAtMs.Clear();
        _currentCurvePoints = new List<MeasurementCurvePoint>();
        _externalDeviceState = ExternalDeviceStateSnapshot.Empty;
        _simulatedTimeMs = 0;
        _activeConcurrentGroupName = null;
        _currentConcurrentEvent = null;
        _activeConcurrentTests.Clear();
        PublishStateSnapshot();
    }

    /// <summary>
    /// Executes PublishStepEvaluation.
    /// </summary>
    private void PublishStepEvaluation(Test test, TestOutcome outcome, double? measured = null, double? lower = null, double? upper = null, string? unit = null, string? details = null, IReadOnlyList<StepConnectionTrace>? traces = null, IReadOnlyList<MeasurementCurvePoint>? curvePoints = null, string? stepNameOverride = null)
    {
        var stepName = stepNameOverride ?? test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        var resolvedTraces = traces;
        if (resolvedTraces == null || resolvedTraces.Count == 0)
        {
            resolvedTraces = CollectTestTraces(test);
        }

        _observer.OnStepEvaluated(test, new StepEvaluation(stepName, outcome, measured, lower, upper, unit, details, resolvedTraces, curvePoints ?? CaptureCurvePoints()));
        PublishStateSnapshot(force: true);
    }

    /// <summary>
    /// Executes BuildWaveformCurvePoints.
    /// </summary>
    private IReadOnlyList<MeasurementCurvePoint> BuildWaveformCurvePoints(ActiveWaveformSession? session)
    {
        if (session == null)
        {
            return CaptureCurvePoints();
        }

        var points = new List<MeasurementCurvePoint>();
        foreach (var monitor in session.Monitors)
        {
            foreach (var sample in monitor.Samples)
            {
                var absoluteTimeMs = session.StartTimeMs + (long)Math.Round(sample.TimeMs, MidpointRounding.AwayFromZero);
                var stimulusValue = monitor.Waveform.GetValueAt(sample.TimeMs);
                points.Add(new MeasurementCurvePoint(absoluteTimeMs, $"WF {monitor.StimulusSignal}", stimulusValue, "V"));
                points.Add(new MeasurementCurvePoint(absoluteTimeMs, $"WF {monitor.ObserveSignal}", sample.Value, "V"));
            }
        }

        return points.Count == 0 ? CaptureCurvePoints() : points;
    }

    /// <summary>
    /// Executes ResolveMeasurementBusSignal.
    /// </summary>
    private string? ResolveMeasurementBusSignal(string busName)
    {
        return _measurementBusSignals.TryGetValue(busName, out var signal) ? signal : null;
    }

    /// <summary>
    /// Executes WriteSignal.
    /// </summary>
    private void WriteSignal(string signalName, object? value)
    {
        var normalized = ExtractSignalName(signalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (TryGetNumericValue(value, out var numeric))
        {
            var limitIssues = _wireVizResolver?.CheckOutputLimits(normalized!, numeric, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults);
            if (limitIssues != null && limitIssues.Count > 0)
            {
                throw new SimulationAbortException(SimulationAbortReason.OutputLimitViolation, string.Join(" | ", limitIssues));
            }
        }

        RememberSignal(normalized!, value);
        TryWriteExternalSignal(normalized, value);
    }

    /// <summary>
    /// Executes TryGetNumericValue.
    /// </summary>
    private static bool TryGetNumericValue(object? value, out double numeric)
    {
        switch (value)
        {
            case double d:
                numeric = d;
                return true;
            case float f:
                numeric = f;
                return true;
            case int i:
                numeric = i;
                return true;
            case long l:
                numeric = l;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                numeric = parsed;
                return true;
            default:
                numeric = 0d;
                return false;
        }
    }

    /// <summary>
    /// Executes TryReadSignal.
    /// </summary>
    private bool TryReadSignal(string signalName, out object? value, out string? details)
    {
        details = null;
        var normalized = ExtractSignalName(signalName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return false;
        }

        var directlyForced = _faults.TryGetForcedSignal(normalized);
        if (directlyForced.HasValue)
        {
            value = _faults.ApplySignalDrift(normalized, directlyForced.Value);
            details = "Quelle: Fault Injection";
            return true;
        }

        if (_externalDeviceSession != null && _wireVizResolver != null &&
            _wireVizResolver.TryResolveRuntimeTargets(normalized, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, false, out var runtimeTargets))
        {
            if (_externalDeviceSession.TryReadSignal(runtimeTargets, _cancellationToken, out var resolvedSignal, out value, out var stateSummary, out var error, _simulatedTimeMs))
            {
                RefreshExternalDeviceState();
                var forcedValue = _faults.TryGetForcedSignal(resolvedSignal) ?? _faults.TryGetForcedSignal(normalized);
                if (forcedValue.HasValue)
                {
                    value = _faults.ApplySignalDrift(resolvedSignal, forcedValue.Value);
                }
                else if (value is double numeric)
                {
                    value = _faults.ApplySignalDrift(resolvedSignal, numeric);
                }
                else if (value is float numericFloat)
                {
                    value = _faults.ApplySignalDrift(resolvedSignal, numericFloat);
                }
                else if (value is int numericInt)
                {
                    value = _faults.ApplySignalDrift(resolvedSignal, numericInt);
                }
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

    /// <summary>
    /// Executes EvaluateNumericOutcome.
    /// </summary>
    private static TestOutcome EvaluateNumericOutcome(double? measured, double? lowerLimit, double? upperLimit)
    {
        if (!measured.HasValue)
        {
            return TestOutcome.Error;
        }

        if (lowerLimit.HasValue && upperLimit.HasValue && lowerLimit.Value > upperLimit.Value)
        {
            (lowerLimit, upperLimit) = (upperLimit, lowerLimit);
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

    /// <summary>
    /// Executes FormatLimit.
    /// </summary>
    private static string FormatLimit(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
    }

    /// <summary>
    /// Executes ParseEngineeringValue.
    /// </summary>
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
            "us" => value / 1_000_000d,
            "µs" => value / 1_000_000d,
            "ns" => value / 1_000_000_000d,
            _ => value
        };
    }

    /// <summary>
    /// Executes GetParameterAttribute.
    /// </summary>
    private static string? GetParameterAttribute(TestParameters? parameters, string attributeName)
    {
        if (parameters?.AdditionalAttributes == null || string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        var attribute = parameters.AdditionalAttributes.FirstOrDefault(item =>
            string.Equals(item.Name, attributeName, StringComparison.OrdinalIgnoreCase));
        return attribute?.Value;
    }

    /// <summary>
    /// Executes ParseYesNo.
    /// </summary>
    private static bool ParseYesNo(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var normalized = value.Trim().Trim('\'', '"');
        return normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes ParseExpectedExitCode.
    /// </summary>
    private static int ParseExpectedExitCode(TestParameters parameters)
    {
        var candidates = new[] { "ExpectedExitCode", "ExitCode", "PassExitCode" };
        foreach (var candidate in candidates)
        {
            var raw = GetParameterAttribute(parameters, candidate);
            if (int.TryParse(raw?.Trim().Trim('\'', '"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    /// <summary>
    /// Executes ParseDurationMilliseconds.
    /// </summary>
    private static long ParseDurationMilliseconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0L;
        }

        var trimmed = raw.Trim().Trim('\'', '"');
        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\s*([-+]?\d+(?:[.,]\d+)?)\s*([A-Za-zµ]*)\s*$");
        if (!match.Success)
        {
            return 0L;
        }

        var numericText = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return 0L;
        }

        var unit = match.Groups[2].Value.Trim().ToLowerInvariant();
        var milliseconds = unit switch
        {
            "" => number,
            "ms" => number,
            "s" => number * 1000d,
            "us" => number / 1000d,
            "µs" => number / 1000d,
            _ => number
        };

        return (long)Math.Max(0d, Math.Round(milliseconds, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Executes BuildExecutableStartInfo.
    /// </summary>
    private ProcessStartInfo BuildExecutableStartInfo(string executablePath)
    {
        var extension = Path.GetExtension(executablePath);
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? _context.ProgramDirectory;
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{executablePath}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{executablePath}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    /// <summary>
    /// Executes EnumerateSignalCandidates.
    /// </summary>
    private IEnumerable<string> EnumerateSignalCandidates(Record record)
    {
        var candidates = new[]
        {
            record.DrawingReference,
            record.TestPoint,
            record.Text,
            record.Expression,
            record.Destination
        };

        foreach (var candidate in candidates)
        {
            var normalized = ExtractSignalName(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }

        foreach (var candidate in EnumerateRecordAttributeSignals(record))
        {
            var normalized = ExtractSignalName(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    /// <summary>
    /// Executes EnumerateRecordAttributeSignals.
    /// </summary>
    private IEnumerable<string> EnumerateRecordAttributeSignals(Record record)
    {
        if (record.AdditionalAttributes == null || record.AdditionalAttributes.Length == 0)
        {
            yield break;
        }

        var attributeNames = new[]
        {
            "ChannelName",
            "Signal",
            "Source",
            "Target",
            "SCL",
            "SDA",
            "MOSI",
            "MISO",
            "CLK",
            "CS",
            "CSST",
            "UIFSignal"
        };

        foreach (var name in attributeNames)
        {
            var raw = ReadAttributeValue(record.AdditionalAttributes, name);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                yield return raw!;
            }
        }
    }

    /// <summary>
    /// Executes ReadAttributeValue.
    /// </summary>
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

    /// <summary>
    /// Executes CollectTestTraces.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace> CollectTestTraces(Test test)
    {
        var result = new List<StepConnectionTrace>();
        if (_wireVizResolver == null || test.Parameters == null)
        {
            return result;
        }

        var parameters = test.Parameters;
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? raw)
        {
            var normalized = ExtractSignalName(raw);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized!);
            }
        }

        AddCandidate(parameters.DrawingReference);
        AddCandidate(parameters.Message);

        foreach (var record in parameters.Records)
        {
            foreach (var candidate in EnumerateSignalCandidates(record))
            {
                candidates.Add(candidate);
            }
        }

        foreach (var table in parameters.Tables)
        {
            foreach (var record in table.Records)
            {
                foreach (var candidate in EnumerateSignalCandidates(record))
                {
                    candidates.Add(candidate);
                }
            }
        }

        if (parameters.ExtraElements != null)
        {
            foreach (var element in parameters.ExtraElements)
            {
                var channelName = element.GetAttribute("ChannelName");
                AddCandidate(channelName);
                AddCandidate(element.GetAttribute("Signal"));
                AddCandidate(element.GetAttribute("Source"));
                AddCandidate(element.GetAttribute("Target"));
                AddCandidate(element.GetAttribute("SCL"));
                AddCandidate(element.GetAttribute("SDA"));
                AddCandidate(element.GetAttribute("MOSI"));
                AddCandidate(element.GetAttribute("MISO"));
                AddCandidate(element.GetAttribute("CLK"));
                AddCandidate(element.GetAttribute("CS"));
                AddCandidate(element.GetAttribute("CSST"));
                AddCandidate(element.GetAttribute("UIFSignal"));
            }
        }

        foreach (var candidate in candidates)
        {
            result.AddRange(CollectSignalTraces(candidate, "Messpfad"));
        }

        return result
            .DistinctBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Executes ExtractSignalName.
    /// </summary>
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

    /// <summary>
    /// Executes AssignInterfaceResponse.
    /// </summary>
    private void AssignInterfaceResponse(Record record, object? responsePayload)
    {
        if (string.IsNullOrWhiteSpace(record.Variable))
        {
            return;
        }

        if (TrySplitInterfaceArray(responsePayload, out var values))
        {
            _context.SetValue(record.Variable!, new ArrayAllocation(values.Count));
            for (var index = 0; index < values.Count; index++)
            {
                _context.SetValue($"{record.Variable}[{index + 1}]", values[index]);
            }

            return;
        }

        _context.SetValue(record.Variable!, responsePayload);
    }

    /// <summary>
    /// Executes TrySplitInterfaceArray.
    /// </summary>
    private static bool TrySplitInterfaceArray(object? responsePayload, out List<object?> values)
    {
        values = new List<object?>();
        switch (responsePayload)
        {
            case System.Text.Json.Nodes.JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    values.Add(ParseInterfaceScalar(item?.ToString()));
                }

                return values.Count > 0;

            case string text:
                var parts = text
                    .Trim()
                    .Trim('[', ']', '{', '}')
                    .Split(new[] { ',', ';', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0)
                    .ToList();

                if (parts.Count <= 1)
                {
                    return false;
                }

                values.AddRange(parts.Select(ParseInterfaceScalar));
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Executes ParseInterfaceScalar.
    /// </summary>
    private static object? ParseInterfaceScalar(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim().Trim('"', '\'');
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
        {
            return current;
        }

        return trimmed;
    }

    /// <summary>
    /// Executes DescribeInterfaceResponse.
    /// </summary>
    private static string DescribeInterfaceResponse(object? responsePayload)
    {
        return responsePayload switch
        {
            null => string.Empty,
            System.Text.Json.Nodes.JsonNode node => node.ToJsonString(),
            _ => responsePayload.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Executes GetRecordAttribute.
    /// </summary>
    private static string? GetRecordAttribute(Record record, string attributeName)
    {
        if (record.AdditionalAttributes == null || string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        var attribute = record.AdditionalAttributes.FirstOrDefault(item =>
            string.Equals(item.Name, attributeName, StringComparison.OrdinalIgnoreCase));
        return attribute?.Value?.Trim().Trim('\'', '"');
    }

    /// <summary>
    /// Initializes a new instance of static.
    /// </summary>
    private static (bool send, bool receive) NormalizeInterfaceDirection(string? rawDirection)
    {
        var direction = rawDirection?.Trim().Trim('\'', '"') ?? string.Empty;
        if (direction.Length == 0)
        {
            return (true, false);
        }

        var normalized = direction.ToLowerInvariant();
        return (
            send: normalized.Contains("send", StringComparison.Ordinal),
            receive: normalized.Contains("receive", StringComparison.Ordinal));
    }

    /// <summary>
    /// Executes CreateExternalDeviceSession.
    /// </summary>
    private ExternalDeviceSession? CreateExternalDeviceSession()
    {
        var pipeName = Environment.GetEnvironmentVariable("CT3XX_PY_DEVICE_PIPE");
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return null;
        }

        return new ExternalDeviceSession(new PythonDeviceSimulatorClient(pipeName));
    }

    /// <summary>
    /// Executes TryAnnounceExternalDevice.
    /// </summary>
    private void TryAnnounceExternalDevice()
    {
        try
        {
            var response = _externalDeviceSession!.Hello(_cancellationToken, _simulatedTimeMs);
            if (response.Ok)
            {
                var deviceResistors = ParseDeviceCtctResistors(response.Result);
                if (deviceResistors.Count > 0)
                {
                    _deviceCtctResistors = deviceResistors;
                    if (_fileSet != null)
                    {
                        _wireVizResolver = WireVizHarnessResolver.Create(_fileSet, _deviceCtctResistors);
                    }
                }

                RefreshExternalDeviceState();
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

    /// <summary>
    /// Executes ParseDeviceCtctResistors.
    /// </summary>
    private static IReadOnlyList<ResistorElementDefinition> ParseDeviceCtctResistors(JsonNode? payload)
    {
        if (payload is not JsonObject payloadObject)
        {
            return Array.Empty<ResistorElementDefinition>();
        }

        if (payloadObject["ctct"] is not JsonObject ctctObject)
        {
            return Array.Empty<ResistorElementDefinition>();
        }

        if (ctctObject["resistances"] is not JsonArray resistanceArray)
        {
            return Array.Empty<ResistorElementDefinition>();
        }

        var results = new List<ResistorElementDefinition>();
        var index = 0;
        foreach (var entry in resistanceArray)
        {
            if (entry is not JsonObject item)
            {
                continue;
            }

            var a = item["a"]?.GetValue<string>()?.Trim();
            var b = item["b"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                continue;
            }

            double? ohmsRaw = null;
            if (item["ohms"] is JsonValue ohmsValue && ohmsValue.TryGetValue<double>(out var numeric))
            {
                ohmsRaw = numeric;
            }
            else if (double.TryParse(item["ohms"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                ohmsRaw = parsed;
            }
            if (!ohmsRaw.HasValue)
            {
                continue;
            }

            var id = item["id"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"DUT_CTCT_R{++index}";
            }

            var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "device_profile"
            };
            results.Add(new ResistorElementDefinition(id, a!, b!, Math.Max(0d, ohmsRaw.Value), metadata));
        }

        return results;
    }

    /// <summary>
    /// Executes TrySetExternalSignal.
    /// </summary>
    private void TrySetExternalSignal(string signalName, object? value)
    {
        RememberSignal(signalName, value);
        if (_externalDeviceSession == null)
        {
            return;
        }

        if (_externalDeviceSession.TryWriteSignal(signalName, value, _cancellationToken, out var error, _simulatedTimeMs))
        {
            RefreshExternalDeviceState();
            _observer.OnMessage($"Python device input <= {signalName} = {_evaluator.ToText(value)}");
        }
        else if (!string.IsNullOrWhiteSpace(error))
        {
            _observer.OnMessage($"Python device input error for {signalName}: {error}");
        }
    }

    /// <summary>
    /// Executes RememberSignal.
    /// </summary>
    private void RememberSignal(string signalName, object? value)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return;
        }

        _signalState[signalName.Trim()] = value;
        _signalChangedAtMs[signalName.Trim()] = _simulatedTimeMs;
        PublishStateSnapshot();
    }

    /// <summary>
    /// Executes PublishStateSnapshot.
    /// </summary>
    private void PublishStateSnapshot(bool force = false)
    {
        if (!force)
        {
            return;
        }

        var signalSnapshot = _signalState
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => _evaluator.ToText(item.Value),
                StringComparer.OrdinalIgnoreCase);

        var measurementBusSnapshot = _measurementBusSignals
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase);

        var relayStates = _wireVizResolver?.DescribeRelayStates(_signalState, _signalChangedAtMs, _simulatedTimeMs, _faults) ?? Array.Empty<string>();
        var elementStates = _wireVizResolver?.DescribeElementStates(_signalState, _signalChangedAtMs, _simulatedTimeMs, _faults) ?? Array.Empty<string>();
        var concurrentBranches = BuildConcurrentBranchSnapshots();
        _observer.OnStateChanged(new SimulationStateSnapshot(
            _currentStepName,
            _simulatedTimeMs,
            signalSnapshot,
            measurementBusSnapshot,
            relayStates,
            _faults.DescribeActiveFaults(),
            _externalDeviceState,
            elementStates,
            _activeConcurrentGroupName,
            _currentConcurrentEvent,
            concurrentBranches));
        _executionController.WaitAfterSnapshot(new SimulationStateSnapshot(
            _currentStepName,
            _simulatedTimeMs,
            signalSnapshot,
            measurementBusSnapshot,
            relayStates,
            _faults.DescribeActiveFaults(),
            _externalDeviceState,
            elementStates,
            _activeConcurrentGroupName,
            _currentConcurrentEvent,
            concurrentBranches), _cancellationToken);
    }

    /// <summary>
    /// Executes BuildConcurrentBranchSnapshots.
    /// </summary>
    private IReadOnlyList<ConcurrentBranchSnapshot> BuildConcurrentBranchSnapshots()
    {
        return _concurrentBranchStates
            .Select(state => new ConcurrentBranchSnapshot(
                state.BranchName,
                state.CurrentItem,
                state.Status,
                state.WaitUntilTimeMs,
                state.Details))
            .ToList();
    }

    /// <summary>
    /// Executes CollectSignalTraces.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace> CollectSignalTraces(string signalName, string traceKind)
    {
        if (_wireVizResolver == null || string.IsNullOrWhiteSpace(signalName))
        {
            return Array.Empty<StepConnectionTrace>();
        }

        if (_wireVizResolver.TryTrace(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, out var traces))
        {
            return traces
                .Select(trace => new StepConnectionTrace($"{traceKind}: {trace.SignalName}", trace.Nodes))
                .ToList();
        }

        if (!_wireVizResolver.TryResolveRuntimeTargets(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, true, out var writeTargets) &&
            !_wireVizResolver.TryResolveRuntimeTargets(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, false, out writeTargets))
        {
            return Array.Empty<StepConnectionTrace>();
        }

        return writeTargets
            .Select(target => new StepConnectionTrace(
                $"{traceKind}: {signalName}",
                new[]
                {
                    signalName,
                    target.SignalName
                }))
            .DistinctBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Executes CollectRecordTraces.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace> CollectRecordTraces(Record record)
    {
        var result = new List<StepConnectionTrace>();
        foreach (var candidate in EnumerateSignalCandidates(record).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result.AddRange(CollectSignalTraces(candidate, "Messpfad"));
        }

        return result;
    }

    /// <summary>
    /// Executes HandlesOwnChildSequence.
    /// </summary>
    private static bool HandlesOwnChildSequence(Test test)
    {
        var id = test.Id?.Trim();
        return string.Equals(id, "2ARB", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(id, "SA1T", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(id, "TRGA", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes StartActiveWaveformSession.
    /// </summary>
    private void StartActiveWaveformSession(IReadOnlyList<(AppliedWaveform Waveform, string StimulusSignal, string? ObserveSignal, string ExternalStimulusSignal, string? ExternalObserveSignal, JsonObject? Response)> channelRuns)
    {
        var monitors = channelRuns
            .Where(item => !string.IsNullOrWhiteSpace(item.ObserveSignal))
            .Select(item => new ActiveWaveformMonitor(item.Waveform, item.StimulusSignal, item.ObserveSignal!))
            .ToList();
        var sampleStepMs = Math.Max(1L, (long)Math.Round(Math.Max(channelRuns.Select(item => item.Waveform.SampleTimeMs).Where(item => item > 0d).DefaultIfEmpty(10d).Min(), 10d), MidpointRounding.AwayFromZero));
        _activeWaveformSession = new ActiveWaveformSession(_simulatedTimeMs, sampleStepMs, monitors);
    }

    /// <summary>
    /// Executes EndActiveWaveformSession.
    /// </summary>
    private void EndActiveWaveformSession()
    {
        _activeWaveformSession = null;
    }

    /// <summary>
    /// Executes SampleActiveWaveformSignals.
    /// </summary>
    private void SampleActiveWaveformSignals()
    {
        var session = _activeWaveformSession;
        if (session == null)
        {
            return;
        }

        var relativeTimeMs = Math.Max(0d, _simulatedTimeMs - session.StartTimeMs);
        foreach (var monitor in session.Monitors)
        {
            if (monitor.LastSampleTimeMs == _simulatedTimeMs)
            {
                continue;
            }

            var currentStimulus = monitor.Waveform.GetValueAt(relativeTimeMs);
            RememberSignal(monitor.StimulusSignal, currentStimulus);
            RecordCurvePoint($"WF {monitor.StimulusSignal}", currentStimulus, "V");

            double? observedNumeric = null;
            if (TryReadSignal(monitor.ObserveSignal, out var observedValue, out _))
            {
                observedNumeric = _evaluator.ToDouble(observedValue);
                RecordCurvePoint($"WF {monitor.ObserveSignal}", observedNumeric, "V");
            }

            monitor.LastSampleTimeMs = _simulatedTimeMs;
            monitor.LastObservedValue = observedNumeric;
            monitor.Samples.Add(new WaveformPoint(relativeTimeMs, observedNumeric ?? 0d));
        }
    }

    /// <summary>
    /// Executes EvaluateWaveformOutcome.
    /// </summary>
    private TestOutcome EvaluateWaveformOutcome(
        ActiveWaveformSession? session,
        IReadOnlyList<(AppliedWaveform Waveform, string StimulusSignal, string? ObserveSignal, string ExternalStimulusSignal, string? ExternalObserveSignal, JsonObject? Response)> channelRuns,
        out string details,
        out double? lastObserved)
    {
        details = string.Empty;
        lastObserved = null;
        var messages = new List<string>();
        var outcome = TestOutcome.Pass;
        if (session == null)
        {
            return TestOutcome.Error;
        }

        foreach (var channel in channelRuns)
        {
            if (!session.TryGetMonitor(channel.ObserveSignal, out var monitor))
            {
                continue;
            }

            lastObserved = monitor.LastObservedValue ?? lastObserved;
            var channelIndex = GetWaveformChannelIndex(channel.Waveform.Metadata);
            var evaluation = EvaluateWaveformChannel(channel.Waveform.Metadata, channelIndex, monitor.Samples);
            messages.Add($"CH{channelIndex + 1}: {evaluation.Message}");
            outcome = CombineOutcomes(outcome, evaluation.Outcome);
        }

        details = string.Join("; ", messages);
        return outcome;
    }

    /// <summary>
    /// Executes CombineOutcomes.
    /// </summary>
    private static TestOutcome CombineOutcomes(TestOutcome current, TestOutcome next)
    {
        if (current == TestOutcome.Error || next == TestOutcome.Error)
        {
            return TestOutcome.Error;
        }

        if (current == TestOutcome.Fail || next == TestOutcome.Fail)
        {
            return TestOutcome.Fail;
        }

        return TestOutcome.Pass;
    }

    /// <summary>
    /// Executes GetWaveformChannelIndex.
    /// </summary>
    private static int GetWaveformChannelIndex(IReadOnlyDictionary<string, string> metadata)
    {
        return metadata.TryGetValue("CHANNEL_INDEX", out var text) &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed >= 0
            ? parsed
            : 0;
    }

    /// <summary>
    /// Executes EvaluateWaveformChannel.
    /// </summary>
    private static WaveformEvaluationResult EvaluateWaveformChannel(IReadOnlyDictionary<string, string> metadata, int channelIndex, IReadOnlyList<WaveformPoint> samples)
    {
        if (samples.Count == 0)
        {
            return new WaveformEvaluationResult(TestOutcome.Error, "keine Ruecksignal-Samples");
        }

        var values = samples.Select(item => item.Value).ToList();
        var max = values.Max();
        var min = values.Min();
        var average = values.Average();
        var rms = Math.Sqrt(values.Average(item => item * item));
        var threshold = (max + min) / 2d;
        var pulseCount = CountPulses(samples, threshold);
        var avgPulseWidthMs = CalculateAveragePulseWidthMs(samples, threshold);

        var messages = new List<string>();
        var outcome = TestOutcome.Pass;
        ApplyMetricCheck("UMAX", max, "V");
        ApplyMetricCheck("UMIN", min, "V");
        ApplyMetricCheck("UAVG", average, "V");
        ApplyMetricCheck("UEFF", rms, "V");
        ApplyMetricCheck("NPUL", pulseCount, string.Empty);
        ApplyMetricCheck("AWID", avgPulseWidthMs, "ms");
        return new WaveformEvaluationResult(outcome, string.Join(", ", messages));

        void ApplyMetricCheck(string metricName, double actualValue, string defaultUnit)
        {
            if (!IsWaveformMetricEnabled(metadata, metricName, channelIndex))
            {
                return;
            }

            var lower = GetWaveformMetricLimit(metadata, "Lower" + metricName, channelIndex, defaultUnit);
            var upper = GetWaveformMetricLimit(metadata, "Upper" + metricName, channelIndex, defaultUnit);
            if (!lower.HasValue && !upper.HasValue)
            {
                messages.Add($"{metricName}={actualValue.ToString("0.###", CultureInfo.InvariantCulture)}{defaultUnit}");
                return;
            }

            var metricOutcome = EvaluateNumericOutcome(actualValue, lower, upper);
            outcome = CombineOutcomes(outcome, metricOutcome);
            messages.Add($"{metricName}={actualValue.ToString("0.###", CultureInfo.InvariantCulture)}{defaultUnit} [{FormatLimit(lower)}..{FormatLimit(upper)}]");
        }
    }

    /// <summary>
    /// Executes CountPulses.
    /// </summary>
    private static int CountPulses(IReadOnlyList<WaveformPoint> samples, double threshold)
    {
        var count = 0;
        var previousHigh = false;
        foreach (var sample in samples)
        {
            var currentHigh = sample.Value >= threshold;
            if (currentHigh && !previousHigh)
            {
                count++;
            }

            previousHigh = currentHigh;
        }

        return count;
    }

    /// <summary>
    /// Executes CalculateAveragePulseWidthMs.
    /// </summary>
    private static double CalculateAveragePulseWidthMs(IReadOnlyList<WaveformPoint> samples, double threshold)
    {
        var widths = new List<double>();
        double? pulseStart = null;
        foreach (var sample in samples)
        {
            var currentHigh = sample.Value >= threshold;
            if (currentHigh && pulseStart == null)
            {
                pulseStart = sample.TimeMs;
            }
            else if (!currentHigh && pulseStart.HasValue)
            {
                widths.Add(Math.Max(0d, sample.TimeMs - pulseStart.Value));
                pulseStart = null;
            }
        }

        if (pulseStart.HasValue)
        {
            widths.Add(Math.Max(0d, samples[^1].TimeMs - pulseStart.Value));
        }

        return widths.Count == 0 ? 0d : widths.Average();
    }

    /// <summary>
    /// Executes IsWaveformMetricEnabled.
    /// </summary>
    private static bool IsWaveformMetricEnabled(IReadOnlyDictionary<string, string> metadata, string metricName, int channelIndex)
    {
        var key = $"Disable{metricName}:{channelIndex}";
        return !metadata.TryGetValue(key, out var value) || !value.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes GetWaveformMetricLimit.
    /// </summary>
    private static double? GetWaveformMetricLimit(IReadOnlyDictionary<string, string> metadata, string keyBase, int channelIndex, string defaultUnit)
    {
        var key = $"{keyBase}:{channelIndex}";
        if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ArbitraryWaveformLoader.ParseEngineeringValue(value, string.IsNullOrWhiteSpace(defaultUnit) ? string.Empty : defaultUnit);
    }

    private sealed class ActiveWaveformSession
    {
        private readonly Dictionary<string, ActiveWaveformMonitor> _monitorsBySignal;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActiveWaveformSession"/> class.
        /// </summary>
        public ActiveWaveformSession(long startTimeMs, long sampleStepMs, IReadOnlyList<ActiveWaveformMonitor> monitors)
        {
            StartTimeMs = startTimeMs;
            SampleStepMs = sampleStepMs;
            Monitors = monitors;
            _monitorsBySignal = monitors.ToDictionary(item => item.ObserveSignal, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the start time ms.
        /// </summary>
        public long StartTimeMs { get; }
        /// <summary>
        /// Gets the sample step ms.
        /// </summary>
        public long SampleStepMs { get; }
        /// <summary>
        /// Gets the monitors.
        /// </summary>
        public IReadOnlyList<ActiveWaveformMonitor> Monitors { get; }

        /// <summary>
        /// Attempts to get monitor.
        /// </summary>
        public bool TryGetMonitor(string? signalName, out ActiveWaveformMonitor monitor)
        {
            if (!string.IsNullOrWhiteSpace(signalName) && _monitorsBySignal.TryGetValue(signalName, out monitor!))
            {
                return true;
            }

            monitor = null!;
            return false;
        }
    }

    private sealed class ActiveWaveformMonitor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActiveWaveformMonitor"/> class.
        /// </summary>
        public ActiveWaveformMonitor(AppliedWaveform waveform, string stimulusSignal, string observeSignal)
        {
            Waveform = waveform;
            StimulusSignal = stimulusSignal;
            ObserveSignal = observeSignal;
        }

        /// <summary>
        /// Gets the waveform.
        /// </summary>
        public AppliedWaveform Waveform { get; }
        /// <summary>
        /// Gets the stimulus signal.
        /// </summary>
        public string StimulusSignal { get; }
        /// <summary>
        /// Gets the observe signal.
        /// </summary>
        public string ObserveSignal { get; }
        /// <summary>
        /// Executes new.
        /// </summary>
        public List<WaveformPoint> Samples { get; } = new();
        /// <summary>
        /// Gets the last sample time ms.
        /// </summary>
        public long LastSampleTimeMs { get; set; } = long.MinValue;
        /// <summary>
        /// Gets the last observed value.
        /// </summary>
        public double? LastObservedValue { get; set; }
    }

    /// <summary>
    /// Executes WaveformEvaluationResult.
    /// </summary>
    private readonly record struct WaveformEvaluationResult(TestOutcome Outcome, string Message);

    /// <summary>
    /// Executes AdvanceTime.
    /// </summary>
    private void AdvanceTime(long milliseconds)
    {
        var remaining = Math.Max(0L, milliseconds);
        if (_activeWaveformSession == null)
        {
            _simulatedTimeMs += remaining;
            PublishStateSnapshot();
            return;
        }

        if (remaining == 0)
        {
            PublishStateSnapshot();
            SampleActiveWaveformSignals();
            return;
        }

        var stepMs = Math.Max(1L, _activeWaveformSession.SampleStepMs);
        while (remaining > 0)
        {
            var slice = Math.Min(stepMs, remaining);
            _simulatedTimeMs += slice;
            remaining -= slice;
            PublishStateSnapshot();
            SampleActiveWaveformSignals();
        }
    }

    /// <summary>
    /// Executes GetStepDurationMs.
    /// </summary>
    private static long GetStepDurationMs(Test test)
    {
        if (test.Parameters?.AdditionalAttributes != null)
        {
            var attribute = test.Parameters.AdditionalAttributes.FirstOrDefault(item =>
                string.Equals(item.Name, "DelayMs", StringComparison.OrdinalIgnoreCase));
            if (attribute != null && long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 100L;
    }

    /// <summary>
    /// Executes RecordCurvePoint.
    /// </summary>
    private void RecordCurvePoint(string label, double? value, string? unit = null)
    {
        _currentCurvePoints.Add(new MeasurementCurvePoint(_simulatedTimeMs, label, value, unit));
    }

    /// <summary>
    /// Executes RefreshExternalDeviceState.
    /// </summary>
    private void RefreshExternalDeviceState()
    {
        if (_externalDeviceSession == null)
        {
            _externalDeviceState = ExternalDeviceStateSnapshot.Empty;
            PublishStateSnapshot();
            return;
        }

        try
        {
            if (_externalDeviceSession.TryReadState(_cancellationToken, out var snapshot, out _, _simulatedTimeMs))
            {
                _externalDeviceState = snapshot;
                PublishStateSnapshot();
            }
        }
        catch
        {
            // Keep last known external state.
        }
    }

    /// <summary>
    /// Executes CaptureCurvePoints.
    /// </summary>
    private IReadOnlyList<MeasurementCurvePoint> CaptureCurvePoints()
    {
        return _currentCurvePoints.ToList();
    }

    private sealed class ConcurrentTestHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentTestHandle"/> class.
        /// </summary>
        public ConcurrentTestHandle(int branchIndex, Test test, Process process, string executablePath, bool evaluateExitCode, int expectedExitCode)
        {
            BranchIndex = branchIndex;
            Test = test;
            Process = process;
            ExecutablePath = executablePath;
            EvaluateExitCode = evaluateExitCode;
            ExpectedExitCode = expectedExitCode;
        }

        /// <summary>
        /// Gets the branch index.
        /// </summary>
        public int BranchIndex { get; }
        /// <summary>
        /// Gets the test.
        /// </summary>
        public Test Test { get; }
        /// <summary>
        /// Gets the process.
        /// </summary>
        public Process Process { get; }
        /// <summary>
        /// Gets the executable path.
        /// </summary>
        public string ExecutablePath { get; }
        /// <summary>
        /// Gets the evaluate exit code.
        /// </summary>
        public bool EvaluateExitCode { get; }
        /// <summary>
        /// Gets the expected exit code.
        /// </summary>
        public int ExpectedExitCode { get; }
    }

    /// <summary>
    /// Executes ConcurrentBranchRuntimeState.
    /// </summary>
    private sealed record ConcurrentBranchRuntimeState(
        int BranchIndex,
        string BranchName,
        string? CurrentItem,
        string Status,
        long? WaitUntilTimeMs = null,
        string? Details = null);
}
