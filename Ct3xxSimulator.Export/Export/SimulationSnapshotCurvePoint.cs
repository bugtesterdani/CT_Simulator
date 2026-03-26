namespace Ct3xxSimulator.Export;

public sealed class SimulationSnapshotCurvePoint
{
    public SimulationSnapshotCurvePoint(long timeMs, string label, double? value, string? unit)
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
