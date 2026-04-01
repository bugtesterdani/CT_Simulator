using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Captures the evaluated result of one logical step within a simulated test.
/// </summary>
public sealed class StepEvaluation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepEvaluation"/> class.
    /// </summary>
    /// <param name="stepName">The display name of the evaluated step.</param>
    /// <param name="outcome">The normalized outcome of the evaluation.</param>
    /// <param name="measuredValue">The primary numeric result value, if one exists.</param>
    /// <param name="lowerLimit">The configured lower limit, if any.</param>
    /// <param name="upperLimit">The configured upper limit, if any.</param>
    /// <param name="unit">The engineering unit of the measured value.</param>
    /// <param name="details">Additional evaluation detail text.</param>
    /// <param name="traces">The resolved connection traces that explain the signal path.</param>
    /// <param name="curvePoints">The chartable samples captured for the step.</param>
    /// <param name="variables">The variable snapshot captured for the step.</param>
    public StepEvaluation(
        string stepName,
        TestOutcome outcome,
        double? measuredValue = null,
        double? lowerLimit = null,
        double? upperLimit = null,
        string? unit = null,
        string? details = null,
        IReadOnlyList<StepConnectionTrace>? traces = null,
        IReadOnlyList<MeasurementCurvePoint>? curvePoints = null,
        IReadOnlyDictionary<string, string>? variables = null)
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
        Variables = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the display name of the evaluated step.
    /// </summary>
    public string StepName { get; }

    /// <summary>
    /// Gets the normalized outcome of the evaluation.
    /// </summary>
    public TestOutcome Outcome { get; }

    /// <summary>
    /// Gets the primary numeric result value of the step, if available.
    /// </summary>
    public double? MeasuredValue { get; }

    /// <summary>
    /// Gets the configured lower limit used for the evaluation, if available.
    /// </summary>
    public double? LowerLimit { get; }

    /// <summary>
    /// Gets the configured upper limit used for the evaluation, if available.
    /// </summary>
    public double? UpperLimit { get; }

    /// <summary>
    /// Gets the engineering unit of the numeric result.
    /// </summary>
    public string? Unit { get; }

    /// <summary>
    /// Gets the formatted detail text of the evaluation.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets the connection traces that contributed to the result.
    /// </summary>
    public IReadOnlyList<StepConnectionTrace> Traces { get; }

    /// <summary>
    /// Gets the chartable samples captured for the step.
    /// </summary>
    public IReadOnlyList<MeasurementCurvePoint> CurvePoints { get; }

    /// <summary>
    /// Gets the variable snapshot captured for the step.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; }
}
