// Provides Simulation Snapshot Curve Point for the export layer export support.
namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation snapshot curve point.
/// </summary>
public sealed class SimulationSnapshotCurvePoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSnapshotCurvePoint"/> class.
    /// </summary>
    public SimulationSnapshotCurvePoint(long timeMs, string label, double? value, string? unit)
    {
        TimeMs = timeMs;
        Label = label;
        Value = value;
        Unit = unit;
    }

    /// <summary>
    /// Gets the time ms.
    /// </summary>
    public long TimeMs { get; }
    /// <summary>
    /// Gets the label.
    /// </summary>
    public string Label { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public double? Value { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string? Unit { get; }
}
