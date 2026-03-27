// Provides Simulation Observer Spy for the simulator test project support code.
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Tests;

internal sealed class SimulationObserverSpy : ISimulationObserver
{
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<string> Messages { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<StepEvaluation> Evaluations { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<SimulationStateSnapshot> Snapshots { get; } = new();

    /// <summary>
    /// Executes on program started.
    /// </summary>
    public void OnProgramStarted(Ct3xxProgram program)
    {
        Messages.Add($"PROGRAM:{program.Id ?? program.ProgramVersion ?? "unknown"}");
    }

    /// <summary>
    /// Executes on group started.
    /// </summary>
    public void OnGroupStarted(Group group)
    {
    }

    /// <summary>
    /// Executes on group completed.
    /// </summary>
    public void OnGroupCompleted(Group group)
    {
    }

    /// <summary>
    /// Executes on group skipped.
    /// </summary>
    public void OnGroupSkipped(Group group, string reason)
    {
        Messages.Add($"SKIP:{group.Name}:{reason}");
    }

    /// <summary>
    /// Executes on loop iteration.
    /// </summary>
    public void OnLoopIteration(int current, int total)
    {
        Messages.Add($"LOOP:{current}/{total}");
    }

    /// <summary>
    /// Executes on test started.
    /// </summary>
    public void OnTestStarted(Test test)
    {
        Messages.Add($"TEST:{test.Parameters?.Name ?? test.Name ?? test.Id ?? "unknown"}");
    }

    /// <summary>
    /// Executes on test completed.
    /// </summary>
    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Messages.Add($"RESULT:{test.Parameters?.Name ?? test.Name ?? test.Id ?? "unknown"}:{outcome}");
    }

    /// <summary>
    /// Executes on message.
    /// </summary>
    public void OnMessage(string message)
    {
        Messages.Add(message);
    }

    /// <summary>
    /// Executes on step evaluated.
    /// </summary>
    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        Evaluations.Add(evaluation);
    }

    /// <summary>
    /// Executes on state changed.
    /// </summary>
    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
        Snapshots.Add(snapshot);
    }
}
