using System;

namespace Ct3xxSimulator.Simulation.WireViz;

/// <summary>
/// Describes one logical runtime target that can be read from or written to by the simulator.
/// </summary>
public sealed class WireVizRuntimeTarget
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizRuntimeTarget"/> class.
    /// </summary>
    /// <param name="signalName">The logical signal name of the runtime target.</param>
    /// <param name="sourceToTargetScale">The scaling factor from source values to target values.</param>
    /// <param name="endpoint">The physical endpoint associated with the target, if any.</param>
    /// <param name="transformDescription">A textual description of the applied transform.</param>
    /// <param name="synthetic">Indicates whether the target is synthetic rather than directly backed by a physical endpoint.</param>
    public WireVizRuntimeTarget(
        string signalName,
        double sourceToTargetScale,
        WireVizEndpoint? endpoint = null,
        string? transformDescription = null,
        bool synthetic = false)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            throw new ArgumentException("Signal name must be provided.", nameof(signalName));
        }

        SignalName = signalName.Trim();
        SourceToTargetScale = sourceToTargetScale;
        Endpoint = endpoint;
        TransformDescription = string.IsNullOrWhiteSpace(transformDescription) ? null : transformDescription.Trim();
        Synthetic = synthetic;
    }

    /// <summary>
    /// Gets the logical signal name of the runtime target.
    /// </summary>
    public string SignalName { get; }
    /// <summary>
    /// Gets the scaling factor from source values to target values.
    /// </summary>
    public double SourceToTargetScale { get; }
    /// <summary>
    /// Gets the physical endpoint associated with the target, if any.
    /// </summary>
    public WireVizEndpoint? Endpoint { get; }
    /// <summary>
    /// Gets the textual description of the applied transform.
    /// </summary>
    public string? TransformDescription { get; }
    /// <summary>
    /// Gets a value indicating whether the target is synthetic.
    /// </summary>
    public bool Synthetic { get; }

    /// <summary>
    /// Converts a source-side value into the corresponding target-side value.
    /// </summary>
    public object? ApplyWrite(object? sourceValue)
    {
        return TryScaleNumeric(sourceValue, SourceToTargetScale, out var scaled)
            ? scaled
            : sourceValue;
    }

    /// <summary>
    /// Converts a target-side value back into the corresponding source-side value.
    /// </summary>
    public object? ApplyRead(object? targetValue)
    {
        if (Math.Abs(SourceToTargetScale) < double.Epsilon)
        {
            return targetValue;
        }

        return TryScaleNumeric(targetValue, 1d / SourceToTargetScale, out var scaled)
            ? scaled
            : targetValue;
    }

    /// <summary>
    /// Executes TryScaleNumeric.
    /// </summary>
    private static bool TryScaleNumeric(object? value, double scale, out object? scaledValue)
    {
        switch (value)
        {
            case null:
                scaledValue = null;
                return false;
            case byte b:
                scaledValue = b * scale;
                return true;
            case short s:
                scaledValue = s * scale;
                return true;
            case int i:
                scaledValue = i * scale;
                return true;
            case long l:
                scaledValue = l * scale;
                return true;
            case float f:
                scaledValue = f * scale;
                return true;
            case double d:
                scaledValue = d * scale;
                return true;
            case decimal m:
                scaledValue = (double)m * scale;
                return true;
            default:
                scaledValue = null;
                return false;
        }
    }
}
