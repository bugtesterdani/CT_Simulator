namespace Ct3xxSimulator.Simulation;

public sealed class MeasurementCurvePoint
{
    public MeasurementCurvePoint(long timeMs, string label, double? value, string? unit = null)
    {
        TimeMs = timeMs;
        Label = label;
        Value = value;
        Unit = unit;
    }

    public long TimeMs { get; }
    public string Label { get; }
    public double? Value { get; }
    public string? Unit { get; }
}
