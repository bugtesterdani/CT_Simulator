// Provides Simulation Snapshot External Device State for the export layer export support.
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation snapshot external device state.
/// </summary>
public sealed class SimulationSnapshotExternalDeviceState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSnapshotExternalDeviceState"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the time ms.
    /// </summary>
    public long TimeMs { get; }
    /// <summary>
    /// Gets the inputs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Inputs { get; }
    /// <summary>
    /// Gets the sources.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sources { get; }
    /// <summary>
    /// Gets the internal signals.
    /// </summary>
    public IReadOnlyDictionary<string, string> InternalSignals { get; }
    /// <summary>
    /// Gets the outputs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Outputs { get; }
    /// <summary>
    /// Gets the interfaces.
    /// </summary>
    public IReadOnlyDictionary<string, string> Interfaces { get; }
}
