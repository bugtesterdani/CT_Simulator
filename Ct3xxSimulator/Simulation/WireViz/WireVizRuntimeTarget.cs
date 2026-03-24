using System;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizRuntimeTarget
{
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

    public string SignalName { get; }
    public double SourceToTargetScale { get; }
    public WireVizEndpoint? Endpoint { get; }
    public string? TransformDescription { get; }
    public bool Synthetic { get; }

    public object? ApplyWrite(object? sourceValue)
    {
        return TryScaleNumeric(sourceValue, SourceToTargetScale, out var scaled)
            ? scaled
            : sourceValue;
    }

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
