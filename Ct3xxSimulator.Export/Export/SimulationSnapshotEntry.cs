using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

public sealed class SimulationSnapshotEntry
{
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

    public int Index { get; }
    public string? CurrentStep { get; }
    public long CurrentTimeMs { get; }
    public IReadOnlyDictionary<string, string> Signals { get; }
    public IReadOnlyDictionary<string, string> MeasurementBuses { get; }
    public IReadOnlyList<string> RelayStates { get; }
    public IReadOnlyList<string> ActiveFaults { get; }
    public SimulationSnapshotExternalDeviceState ExternalDeviceState { get; }
    public IReadOnlyList<string> ElementStates { get; }
    public string? ActiveConcurrentGroup { get; }
    public string? ConcurrentEvent { get; }
    public IReadOnlyList<SimulationSnapshotConcurrentBranch> ConcurrentBranches { get; }
}
