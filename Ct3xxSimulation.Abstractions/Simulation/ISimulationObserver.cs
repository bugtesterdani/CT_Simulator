using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Receives lifecycle, result and state notifications from the simulator runtime.
/// </summary>
public interface ISimulationObserver
{
    /// <summary>
    /// Notifies the observer that a CT3xx program run has started.
    /// </summary>
    /// <param name="program">The loaded CT3xx program definition.</param>
    void OnProgramStarted(Ct3xxProgram program);

    /// <summary>
    /// Notifies the observer that the DUT loop advanced to the next iteration.
    /// </summary>
    /// <param name="iteration">The current one-based iteration number.</param>
    /// <param name="totalIterations">The total number of configured iterations.</param>
    void OnLoopIteration(int iteration, int totalIterations);

    /// <summary>
    /// Notifies the observer that a group started execution.
    /// </summary>
    /// <param name="group">The group that started.</param>
    void OnGroupStarted(Group group);

    /// <summary>
    /// Notifies the observer that a group was skipped.
    /// </summary>
    /// <param name="group">The skipped group.</param>
    /// <param name="reason">The textual reason for skipping the group.</param>
    void OnGroupSkipped(Group group, string reason);

    /// <summary>
    /// Notifies the observer that a group completed execution.
    /// </summary>
    /// <param name="group">The completed group.</param>
    void OnGroupCompleted(Group group);

    /// <summary>
    /// Notifies the observer that a test started execution.
    /// </summary>
    /// <param name="test">The test that started.</param>
    void OnTestStarted(Test test);

    /// <summary>
    /// Notifies the observer that a test completed execution.
    /// </summary>
    /// <param name="test">The completed test.</param>
    /// <param name="outcome">The resulting test outcome.</param>
    void OnTestCompleted(Test test, TestOutcome outcome);

    /// <summary>
    /// Notifies the observer that one logical step within a test was evaluated.
    /// </summary>
    /// <param name="test">The owning test.</param>
    /// <param name="evaluation">The evaluated step result.</param>
    void OnStepEvaluated(Test test, StepEvaluation evaluation);

    /// <summary>
    /// Notifies the observer that the simulator state changed.
    /// </summary>
    /// <param name="snapshot">The new simulator state snapshot.</param>
    void OnStateChanged(SimulationStateSnapshot snapshot);

    /// <summary>
    /// Emits a free-form runtime message.
    /// </summary>
    /// <param name="message">The message text.</param>
    void OnMessage(string message);
}

/// <summary>
/// Writes simulation events to the console for CLI-oriented runs.
/// </summary>
public sealed class ConsoleSimulationObserver : ISimulationObserver
{
    /// <inheritdoc />
    public void OnProgramStarted(Ct3xxProgram program)
    {
        Console.WriteLine($"CT3xx Program: {program.ProgramVersion ?? "unknown version"} | GUID {program.ProgramGuid}");
        if (!string.IsNullOrWhiteSpace(program.DutName))
        {
            Console.WriteLine($"DUT: {program.DutName} ({program.DutRevision})");
        }
    }

    /// <inheritdoc />
    public void OnLoopIteration(int iteration, int totalIterations)
    {
        Console.WriteLine($"== DUT iteration {iteration}/{totalIterations} ==");
    }

    /// <inheritdoc />
    public void OnGroupStarted(Group group)
    {
        Console.WriteLine($"-- Group: {group.Name}");
    }

    /// <inheritdoc />
    public void OnGroupSkipped(Group group, string reason)
    {
        Console.WriteLine($"-- Skipping group '{group.Name}' because {reason}");
    }

    /// <inheritdoc />
    public void OnGroupCompleted(Group group)
    {
    }

    /// <inheritdoc />
    public void OnTestStarted(Test test)
    {
        var title = test.Parameters?.Name ?? "Unnamed Test";
        Console.WriteLine($"   Test {test.Id}: {title}");
    }

    /// <inheritdoc />
    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Console.WriteLine($"      Result: {outcome.ToString().ToUpperInvariant()}");
    }

    /// <inheritdoc />
    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        var unit = string.IsNullOrWhiteSpace(evaluation.Unit) ? string.Empty : $" {evaluation.Unit}";
        var value = evaluation.MeasuredValue?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var lower = evaluation.LowerLimit?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var upper = evaluation.UpperLimit?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        Console.WriteLine($"      Step {evaluation.StepName}: {evaluation.Outcome} | value {value}{unit} | limits {lower} .. {upper}");
    }

    /// <inheritdoc />
    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
        Console.WriteLine($"      State update: {snapshot.Signals.Count} signals, {snapshot.RelayStates.Count} relays");
    }

    /// <inheritdoc />
    public void OnMessage(string message)
    {
        Console.WriteLine($"      {message}");
    }
}

/// <summary>
/// Ignores all simulator notifications.
/// </summary>
public sealed class NullSimulationObserver : ISimulationObserver
{
    /// <inheritdoc />
    public void OnProgramStarted(Ct3xxProgram program) { }
    /// <inheritdoc />
    public void OnLoopIteration(int iteration, int totalIterations) { }
    /// <inheritdoc />
    public void OnGroupStarted(Group group) { }
    /// <inheritdoc />
    public void OnGroupSkipped(Group group, string reason) { }
    /// <inheritdoc />
    public void OnGroupCompleted(Group group) { }
    /// <inheritdoc />
    public void OnTestStarted(Test test) { }
    /// <inheritdoc />
    public void OnTestCompleted(Test test, TestOutcome outcome) { }
    /// <inheritdoc />
    public void OnStepEvaluated(Test test, StepEvaluation evaluation) { }
    /// <inheritdoc />
    public void OnStateChanged(SimulationStateSnapshot snapshot) { }
    /// <inheritdoc />
    public void OnMessage(string message) { }
}
