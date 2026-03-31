// Provides Cli Simulation Observer for the command-line interface support code.
using System.Globalization;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Cli;

internal sealed class CliSimulationObserver : ISimulationObserver
{
    private readonly List<StepEvaluation> _steps = new();
    private readonly List<SimulationExportLogEntry> _logs = new();

    /// <summary>
    /// Gets the steps.
    /// </summary>
    public IReadOnlyList<StepEvaluation> Steps => _steps;
    /// <summary>
    /// Gets the logs.
    /// </summary>
    public IReadOnlyList<SimulationExportLogEntry> Logs => _logs;

    /// <summary>
    /// Executes on program started.
    /// </summary>
    public void OnProgramStarted(Ct3xxProgram program)
    {
        Console.WriteLine($"Programm: {program.ProgramComment ?? program.ProgramGuid}");
    }

    /// <summary>
    /// Executes on loop iteration.
    /// </summary>
    public void OnLoopIteration(int iteration, int totalIterations)
    {
        Console.WriteLine($"== DUT iteration {iteration}/{totalIterations} ==");
    }

    /// <summary>
    /// Executes on group started.
    /// </summary>
    public void OnGroupStarted(Group group)
    {
        Console.WriteLine($"-- Gruppe: {group.Name}");
    }

    /// <summary>
    /// Executes on group skipped.
    /// </summary>
    public void OnGroupSkipped(Group group, string reason)
    {
        Log($"Gruppe uebersprungen: {group.Name} ({reason})");
    }

    /// <summary>
    /// Executes on group completed.
    /// </summary>
    public void OnGroupCompleted(Group group)
    {
    }

    /// <summary>
    /// Executes on test started.
    /// </summary>
    public void OnTestStarted(Test test)
    {
        Console.WriteLine($"> {test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test"}");
    }

    /// <summary>
    /// Executes on test completed.
    /// </summary>
    public void OnTestCompleted(Test test, TestOutcome outcome)
    {
        Console.WriteLine($"  Ergebnis: {outcome}");
    }

    /// <summary>
    /// Executes on step evaluated.
    /// </summary>
    public void OnStepEvaluated(Test test, StepEvaluation evaluation)
    {
        _steps.Add(evaluation);
        var measured = evaluation.MeasuredValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        var lower = evaluation.LowerLimit?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        var upper = evaluation.UpperLimit?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
        Console.WriteLine($"  Schritt {evaluation.StepName}: {evaluation.Outcome} | Wert {measured} | Grenzen {lower} .. {upper} {evaluation.Unit}".Trim());
    }

    /// <summary>
    /// Executes on state changed.
    /// </summary>
    public void OnStateChanged(SimulationStateSnapshot snapshot)
    {
    }

    /// <summary>
    /// Executes on message.
    /// </summary>
    public void OnMessage(string message)
    {
        Log(message);
        Console.WriteLine($"  {message}");
    }

    /// <summary>
    /// Gets the exit code.
    /// </summary>
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

    /// <summary>
    /// Creates the export document.
    /// </summary>
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

    /// <summary>
    /// Executes Log.
    /// </summary>
    private void Log(string message)
    {
        _logs.Add(new SimulationExportLogEntry(DateTime.Now, message));
    }

    /// <summary>
    /// Executes FormatNumber.
    /// </summary>
    private static string FormatNumber(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
}
