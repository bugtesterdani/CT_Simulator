namespace Ct3xxSimulator.Simulation.Waveforms;

public sealed class WaveformPoint
{
    public WaveformPoint(double timeMs, double value)
    {
        TimeMs = timeMs;
        Value = value;
    }

    public double TimeMs { get; }
    public double Value { get; }
}
