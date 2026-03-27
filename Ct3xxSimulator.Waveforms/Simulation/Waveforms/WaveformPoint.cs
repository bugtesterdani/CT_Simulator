namespace Ct3xxSimulator.Simulation.Waveforms;

/// <summary>
/// Represents one time/value sample in a waveform definition.
/// </summary>
public sealed class WaveformPoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WaveformPoint"/> class.
    /// </summary>
    /// <param name="timeMs">The sample time in milliseconds.</param>
    /// <param name="value">The sample value.</param>
    public WaveformPoint(double timeMs, double value)
    {
        TimeMs = timeMs;
        Value = value;
    }

    /// <summary>
    /// Gets the sample time in milliseconds.
    /// </summary>
    public double TimeMs { get; }
    /// <summary>
    /// Gets the sample value.
    /// </summary>
    public double Value { get; }
}
