using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public interface ISimulationObserver
{
    void OnProgramStarted(Ct3xxProgram program);
    void OnLoopIteration(int iteration, int totalIterations);
    void OnGroupStarted(Group group);
    void OnGroupSkipped(Group group, string reason);
    void OnGroupCompleted(Group group);
    void OnTestStarted(Test test);
    void OnTestCompleted(Test test, TestOutcome outcome);
    void OnStepEvaluated(Test test, StepEvaluation evaluation);
    void OnMessage(string message);
}

public sealed class ConsoleSimulationObserver : ISimulationObserver
{
    public void OnProgramStarted(Ct3xxProgram program)
    {
        Console.WriteLine($"CT3xx Program: {program.ProgramVersion ?? "unknown version"} | GUID {program.ProgramGuid}");
        if (!string.IsNullOrWhiteSpace(program.DutName))
        {
            Console.WriteLine($"DUT: {program.DutName} ({program.DutRevision})");
        }
    }

    public void OnLoopIteration(int iteration, int totalIterations)
    {
        Console.WriteLine($"== DUT iteration {iteration}/{totalIterations} ==");
    }

    public void OnGroupStarted(Group group)
    {
        Console.WriteLine($"-- Group: {group.Name}");
    }

    public void OnGroupSkipped(Group group, string reason)
    {
        Console.WriteLine($"-- Skipping group '{group.Name}' because {reason}");
    }

    public void OnGroupCompleted(Group group)
    {
    }

    public void OnTestStarted(Test test)
    {
        var title = test.Parameters?.Name ?? "Unnamed Test";
        Console.WriteLine($"   Test {test.Id}: {title}");
    }

    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Console.WriteLine($"      Result: {outcome.ToString().ToUpperInvariant()}");
    }

    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        var unit = string.IsNullOrWhiteSpace(evaluation.Unit) ? string.Empty : $" {evaluation.Unit}";
        var value = evaluation.MeasuredValue?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var lower = evaluation.LowerLimit?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        var upper = evaluation.UpperLimit?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        Console.WriteLine($"      Step {evaluation.StepName}: {evaluation.Outcome} | value {value}{unit} | limits {lower} .. {upper}");
    }

    public void OnMessage(string message)
    {
        Console.WriteLine($"      {message}");
    }
}

public sealed class NullSimulationObserver : ISimulationObserver
{
    public void OnProgramStarted(Ct3xxProgram program) { }
    public void OnLoopIteration(int iteration, int totalIterations) { }
    public void OnGroupStarted(Group group) { }
    public void OnGroupSkipped(Group group, string reason) { }
    public void OnGroupCompleted(Group group) { }
    public void OnTestStarted(Test test) { }
    public void OnTestCompleted(Test test, TestOutcome outcome) { }
    public void OnStepEvaluated(Test test, StepEvaluation evaluation) { }
    public void OnMessage(string message) { }
}
