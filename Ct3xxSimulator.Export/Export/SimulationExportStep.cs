// Provides Simulation Export Step for the export layer export support.
using System.Collections.Generic;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation export step.
/// </summary>
public sealed class SimulationExportStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationExportStep"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public string StepName { get; }
    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public string Outcome { get; }
    /// <summary>
    /// Gets the measured value.
    /// </summary>
    public string MeasuredValue { get; }
    /// <summary>
    /// Gets the lower limit.
    /// </summary>
    public string LowerLimit { get; }
    /// <summary>
    /// Gets the upper limit.
    /// </summary>
    public string UpperLimit { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string Unit { get; }
    /// <summary>
    /// Gets the details.
    /// </summary>
    public string Details { get; }
    /// <summary>
    /// Gets the traces.
    /// </summary>
    public IReadOnlyList<StepConnectionTrace> Traces { get; }
    /// <summary>
    /// Gets the curve points.
    /// </summary>
    public IReadOnlyList<MeasurementCurvePoint> CurvePoints { get; }
    /// <summary>
    /// Gets the timeline index.
    /// </summary>
    public int? TimelineIndex { get; }
}
