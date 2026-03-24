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
        string? details = null,
        IReadOnlyList<StepConnectionTrace>? traces = null,
        IReadOnlyList<MeasurementCurvePoint>? curvePoints = null)
    {
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        Details = details;
        Traces = traces ?? Array.Empty<StepConnectionTrace>();
        CurvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
    }

    public string StepName { get; }
    public TestOutcome Outcome { get; }
    public double? MeasuredValue { get; }
    public double? LowerLimit { get; }
    public double? UpperLimit { get; }
    public string? Unit { get; }
    public string? Details { get; }
    public IReadOnlyList<StepConnectionTrace> Traces { get; }
    public IReadOnlyList<MeasurementCurvePoint> CurvePoints { get; }
}
