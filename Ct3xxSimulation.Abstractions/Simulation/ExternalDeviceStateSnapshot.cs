using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

public sealed class ExternalDeviceStateSnapshot
{
    public ExternalDeviceStateSnapshot(
        long timeMs,
        IReadOnlyDictionary<string, string>? inputs = null,
        IReadOnlyDictionary<string, string>? sources = null,
        IReadOnlyDictionary<string, string>? internalSignals = null,
        IReadOnlyDictionary<string, string>? outputs = null,
        IReadOnlyDictionary<string, string>? interfaces = null)
    {
        TimeMs = timeMs;
        Inputs = inputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Sources = sources ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        InternalSignals = internalSignals ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Outputs = outputs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Interfaces = interfaces ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public long TimeMs { get; }
    public IReadOnlyDictionary<string, string> Inputs { get; }
    public IReadOnlyDictionary<string, string> Sources { get; }
    public IReadOnlyDictionary<string, string> InternalSignals { get; }
    public IReadOnlyDictionary<string, string> Outputs { get; }
    public IReadOnlyDictionary<string, string> Interfaces { get; }

    public static ExternalDeviceStateSnapshot Empty { get; } = new(0);
}
