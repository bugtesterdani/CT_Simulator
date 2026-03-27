namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Represents a single measured sample that belongs to a chartable step or waveform result.
/// </summary>
public sealed class MeasurementCurvePoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeasurementCurvePoint"/> class.
    /// </summary>
    /// <param name="timeMs">The simulated time of the sample in milliseconds.</param>
    /// <param name="label">The logical signal or series label that owns the sample.</param>
    /// <param name="value">The sampled numeric value, or <see langword="null"/> when the sample is not numeric.</param>
    /// <param name="unit">The engineering unit of <paramref name="value"/>, if available.</param>
    public MeasurementCurvePoint(long timeMs, string label, double? value, string? unit = null)
    {
        TimeMs = timeMs;
        Label = label;
        Value = value;
        Unit = unit;
    }

    /// <summary>
    /// Gets the simulated time of the sample in milliseconds.
    /// </summary>
    public long TimeMs { get; }

    /// <summary>
    /// Gets the logical series label that produced the sample.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the sampled numeric value.
    /// </summary>
    public double? Value { get; }

    /// <summary>
    /// Gets the engineering unit of the sampled value.
    /// </summary>
    public string? Unit { get; }
}
