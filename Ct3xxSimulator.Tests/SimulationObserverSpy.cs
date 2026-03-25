using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Tests;

internal sealed class SimulationObserverSpy : ISimulationObserver
{
    public List<string> Messages { get; } = new();
    public List<StepEvaluation> Evaluations { get; } = new();
    public List<SimulationStateSnapshot> Snapshots { get; } = new();

    public void OnProgramStarted(Ct3xxProgram program)
    {
        Messages.Add($"PROGRAM:{program.Id ?? program.ProgramVersion ?? "unknown"}");
    }

    public void OnGroupStarted(Group group)
    {
    }

    public void OnGroupCompleted(Group group)
    {
    }

    public void OnGroupSkipped(Group group, string reason)
    {
        Messages.Add($"SKIP:{group.Name}:{reason}");
    }

    public void OnLoopIteration(int current, int total)
    {
        Messages.Add($"LOOP:{current}/{total}");
    }

    public void OnTestStarted(Test test)
    {
        Messages.Add($"TEST:{test.Parameters?.Name ?? test.Name ?? test.Id ?? "unknown"}");
    }

    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Messages.Add($"RESULT:{test.Parameters?.Name ?? test.Name ?? test.Id ?? "unknown"}:{outcome}");
    }

    public void OnMessage(string message)
    {
        Messages.Add(message);
    }

    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        Evaluations.Add(evaluation);
    }

    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
        Snapshots.Add(snapshot);
    }
}
