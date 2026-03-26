using System.Collections.Generic;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Export;

public sealed class SimulationExportStep
{
    public SimulationExportStep(
        string stepName,
        string outcome,
        string measuredValue,
        string lowerLimit,
        string upperLimit,
        string unit,
        string details,
        IReadOnlyList<StepConnectionTrace>? traces = null,
        IReadOnlyList<MeasurementCurvePoint>? curvePoints = null,
        int? timelineIndex = null)
    {
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        Details = details;
        Traces = traces ?? new List<StepConnectionTrace>();
        CurvePoints = curvePoints ?? new List<MeasurementCurvePoint>();
        TimelineIndex = timelineIndex;
    }

    public string StepName { get; }
    public string Outcome { get; }
    public string MeasuredValue { get; }
    public string LowerLimit { get; }
    public string UpperLimit { get; }
    public string Unit { get; }
    public string Details { get; }
    public IReadOnlyList<StepConnectionTrace> Traces { get; }
    public IReadOnlyList<MeasurementCurvePoint> CurvePoints { get; }
    public int? TimelineIndex { get; }
}
