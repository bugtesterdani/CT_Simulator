// Provides Step Result View Model for the desktop application view model support.
using System;
using System.Collections.Generic;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the step result view model.
/// </summary>
public sealed class StepResultViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepResultViewModel"/> class.
    /// </summary>
    public StepResultViewModel(
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
        Traces = traces ?? Array.Empty<StepConnectionTrace>();
        CurvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
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
