using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

public sealed class SimulationSnapshotExternalDeviceState
{
    public SimulationSnapshotExternalDeviceState(
        long timeMs,
        IReadOnlyDictionary<string, string> inputs,
        IReadOnlyDictionary<string, string> sources,
        IReadOnlyDictionary<string, string> internalSignals,
        IReadOnlyDictionary<string, string> outputs,
        IReadOnlyDictionary<string, string> interfaces)
    {
        TimeMs = timeMs;
        Inputs = inputs;
        Sources = sources;
        InternalSignals = internalSignals;
        Outputs = outputs;
        Interfaces = interfaces;
    }

    public long TimeMs { get; }
    public IReadOnlyDictionary<string, string> Inputs { get; }
    public IReadOnlyDictionary<string, string> Sources { get; }
    public IReadOnlyDictionary<string, string> InternalSignals { get; }
    public IReadOnlyDictionary<string, string> Outputs { get; }
    public IReadOnlyDictionary<string, string> Interfaces { get; }
}
