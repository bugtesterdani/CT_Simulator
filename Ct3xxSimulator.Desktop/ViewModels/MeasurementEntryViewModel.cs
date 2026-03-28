// Provides Measurement Entry View Model for measurement overview UI.
using System;
using System.Collections.Generic;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents one measurement entry for ICT/CTCT/SHRT overview.
/// </summary>
public sealed class MeasurementEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeasurementEntryViewModel"/> class.
    /// </summary>
    public MeasurementEntryViewModel(
        string testType,
        string stepName,
        string outcome,
        string measuredValue,
        string lowerLimit,
        string upperLimit,
        string unit,
        string contactSummary,
        string details,
        IReadOnlyList<StepConnectionTrace> traces,
        IReadOnlyList<MeasurementCurvePoint> curvePoints)
    {
        TestType = testType;
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        ContactSummary = contactSummary;
        Details = details;
        Traces = traces ?? Array.Empty<StepConnectionTrace>();
        CurvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
    }

    /// <summary>
    /// Gets the test type.
    /// </summary>
    public string TestType { get; }
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
    /// Gets the contact summary text.
    /// </summary>
    public string ContactSummary { get; }
    /// <summary>
    /// Gets the details text.
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
    /// Gets a value indicating whether the entry has traces.
    /// </summary>
    public bool HasTraces => Traces.Count > 0;
}
