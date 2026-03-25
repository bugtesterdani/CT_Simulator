using System.Globalization;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Cli;

internal sealed class CliSimulationObserver : ISimulationObserver
{
    private readonly List<StepEvaluation> _steps = new();
    private readonly List<SimulationExportLogEntry> _logs = new();

    public IReadOnlyList<StepEvaluation> Steps => _steps;
    public IReadOnlyList<SimulationExportLogEntry> Logs => _logs;

    public void OnProgramStarted(Ct3xxProgram program)
    {
        Console.WriteLine($"Programm: {program.ProgramComment ?? program.ProgramGuid}");
    }

    public void OnLoopIteration(int iteration, int totalIterations)
    {
        Console.WriteLine($"== DUT iteration {iteration}/{totalIterations} ==");
    }

    public void OnGroupStarted(Group group)
    {
        Console.WriteLine($"-- Gruppe: {group.Name}");
    }

    public void OnGroupSkipped(Group group, string reason)
    {
        Log($"Gruppe uebersprungen: {group.Name} ({reason})");
    }

    public void OnGroupCompleted(Group group)
    {
    }

    public void OnTestStarted(Test test)
    {
        Console.WriteLine($"> {test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test"}");
    }

    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Console.WriteLine($"  Ergebnis: {outcome}");
    }

    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        _steps.Add(evaluation);
        var measured = evaluation.MeasuredValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        var lower = evaluation.LowerLimit?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        var upper = evaluation.UpperLimit?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        Console.WriteLine($"  Schritt {evaluation.StepName}: {evaluation.Outcome} | Wert {measured} | Grenzen {lower} .. {upper} {evaluation.Unit}".Trim());
    }

    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
    }

    public void OnMessage(string message)
    {
        Log(message);
        Console.WriteLine($"  {message}");
    }

    public int GetExitCode()
    {
        if (_steps.Any(step => step.Outcome == TestOutcome.Error))
        {
            return 2;
        }

        if (_steps.Any(step => step.Outcome == TestOutcome.Fail))
        {
            return 1;
        }

        return 0;
    }

    public SimulationExportDocument CreateExportDocument(string configurationSummary)
    {
        var steps = _steps.Select(step => new SimulationExportStep(
            step.StepName,
            step.Outcome.ToString(),
            FormatNumber(step.MeasuredValue),
            FormatNumber(step.LowerLimit),
            FormatNumber(step.UpperLimit),
            step.Unit ?? string.Empty,
            step.Details ?? string.Empty,
            step.Traces,
            step.CurvePoints)).ToList();

        return new SimulationExportDocument(DateTimeOffset.Now, configurationSummary, steps, _logs.ToList());
    }

    private void Log(string message)
    {
        _logs.Add(new SimulationExportLogEntry(DateTime.Now, message));
    }

    private static string FormatNumber(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
}
