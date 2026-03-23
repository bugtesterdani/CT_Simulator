namespace Ct3xxSimulator.Simulation;

public sealed class StepEvaluation
{
    public StepEvaluation(
        string stepName,
        TestOutcome outcome,
        double? measuredValue = null,
        double? lowerLimit = null,
        double? upperLimit = null,
        string? unit = null,
        string? details = null)
    {
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        Details = details;
    }

    public string StepName { get; }
    public TestOutcome Outcome { get; }
    public double? MeasuredValue { get; }
    public double? LowerLimit { get; }
    public double? UpperLimit { get; }
    public string? Unit { get; }
    public string? Details { get; }
}
