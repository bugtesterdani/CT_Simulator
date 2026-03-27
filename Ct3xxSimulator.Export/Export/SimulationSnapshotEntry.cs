// Provides Simulation Snapshot Entry for the export layer export support.
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation snapshot entry.
/// </summary>
public sealed class SimulationSnapshotEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSnapshotEntry"/> class.
    /// </summary>
    public SimulationSnapshotEntry(
        int index,
        string? currentStep,
        long currentTimeMs,
        IReadOnlyDictionary<string, string> signals,
        IReadOnlyDictionary<string, string> measurementBuses,
        IReadOnlyList<string> relayStates,
        IReadOnlyList<string> activeFaults,
        SimulationSnapshotExternalDeviceState externalDeviceState,
        IReadOnlyList<string> elementStates,
        string? activeConcurrentGroup,
        string? concurrentEvent,
        IReadOnlyList<SimulationSnapshotConcurrentBranch> concurrentBranches)
    {
        Index = index;
        CurrentStep = currentStep;
        CurrentTimeMs = currentTimeMs;
        Signals = signals;
        MeasurementBuses = measurementBuses;
        RelayStates = relayStates;
        ActiveFaults = activeFaults;
        ExternalDeviceState = externalDeviceState;
        ElementStates = elementStates;
        ActiveConcurrentGroup = activeConcurrentGroup;
        ConcurrentEvent = concurrentEvent;
        ConcurrentBranches = concurrentBranches;
    }

    /// <summary>
    /// Gets the index.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// Gets the current step.
    /// </summary>
    public string? CurrentStep { get; }
    /// <summary>
    /// Gets the current time ms.
    /// </summary>
    public long CurrentTimeMs { get; }
    /// <summary>
    /// Gets the signals.
    /// </summary>
    public IReadOnlyDictionary<string, string> Signals { get; }
    /// <summary>
    /// Gets the measurement buses.
    /// </summary>
    public IReadOnlyDictionary<string, string> MeasurementBuses { get; }
    /// <summary>
    /// Gets the relay states.
    /// </summary>
    public IReadOnlyList<string> RelayStates { get; }
    /// <summary>
    /// Gets the active faults.
    /// </summary>
    public IReadOnlyList<string> ActiveFaults { get; }
    /// <summary>
    /// Gets the external device state.
    /// </summary>
    public SimulationSnapshotExternalDeviceState ExternalDeviceState { get; }
    /// <summary>
    /// Gets the element states.
    /// </summary>
    public IReadOnlyList<string> ElementStates { get; }
    /// <summary>
    /// Gets the active concurrent group.
    /// </summary>
    public string? ActiveConcurrentGroup { get; }
    /// <summary>
    /// Gets the concurrent event.
    /// </summary>
    public string? ConcurrentEvent { get; }
    /// <summary>
    /// Gets the concurrent branches.
    /// </summary>
    public IReadOnlyList<SimulationSnapshotConcurrentBranch> ConcurrentBranches { get; }
}
