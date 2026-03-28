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
        int? timelineIndex = null,
        int? csvRowNumber = null,
        string? csvDescription = null,
        string? csvMessage = null,
        string? csvOutcome = null,
        string? csvMeasuredValue = null,
        string? csvLowerLimit = null,
        string? csvUpperLimit = null,
        string? csvMatchReason = null,
        string? csvDisplayMode = null,
        string? csvLogFlags = null,
        bool csvLogExpectedForOutcome = false)
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
        CsvRowNumber = csvRowNumber;
        CsvDescription = csvDescription ?? string.Empty;
        CsvMessage = csvMessage ?? string.Empty;
        CsvOutcome = csvOutcome ?? string.Empty;
        CsvMeasuredValue = csvMeasuredValue ?? string.Empty;
        CsvLowerLimit = csvLowerLimit ?? string.Empty;
        CsvUpperLimit = csvUpperLimit ?? string.Empty;
        CsvMatchReason = csvMatchReason ?? string.Empty;
        CsvDisplayMode = csvDisplayMode ?? "Off";
        CsvLogFlags = csvLogFlags ?? string.Empty;
        CsvLogExpectedForOutcome = csvLogExpectedForOutcome;
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
    /// <summary>
    /// Gets the matched CSV row number, if any.
    /// </summary>
    public int? CsvRowNumber { get; }
    /// <summary>
    /// Gets the matched CSV description, if any.
    /// </summary>
    public string CsvDescription { get; }
    /// <summary>
    /// Gets the matched CSV message, if any.
    /// </summary>
    public string CsvMessage { get; }
    /// <summary>
    /// Gets the matched CSV outcome, if any.
    /// </summary>
    public string CsvOutcome { get; }
    /// <summary>
    /// Gets the matched CSV measured value, if any.
    /// </summary>
    public string CsvMeasuredValue { get; }
    /// <summary>
    /// Gets the matched CSV lower limit, if any.
    /// </summary>
    public string CsvLowerLimit { get; }
    /// <summary>
    /// Gets the matched CSV upper limit, if any.
    /// </summary>
    public string CsvUpperLimit { get; }
    /// <summary>
    /// Gets the textual reason for the CSV match, if any.
    /// </summary>
    public string CsvMatchReason { get; }
    /// <summary>
    /// Gets the configured CSV display mode that was active for this step.
    /// </summary>
    public string CsvDisplayMode { get; }
    /// <summary>
    /// Gets the CT3xx log flags that govern whether the step should appear in the external CSV for the simulated outcome.
    /// </summary>
    public string CsvLogFlags { get; }
    /// <summary>
    /// Gets a value indicating whether the simulated outcome should have been present in the historical CSV according to the CT3xx log flags.
    /// </summary>
    public bool CsvLogExpectedForOutcome { get; }
}
