using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

public sealed class SimulationStateSnapshot
{
    public SimulationStateSnapshot(
        string? currentStep,
        long currentTimeMs,
        IReadOnlyDictionary<string, string> signals,
        IReadOnlyDictionary<string, string> measurementBuses,
        IReadOnlyList<string> relayStates,
        IReadOnlyList<string>? activeFaults = null,
        ExternalDeviceStateSnapshot? externalDeviceState = null,
        IReadOnlyList<string>? elementStates = null,
        string? activeConcurrentGroup = null,
        string? concurrentEvent = null,
        IReadOnlyList<ConcurrentBranchSnapshot>? concurrentBranches = null)
    {
        CurrentStep = currentStep;
        CurrentTimeMs = currentTimeMs;
        Signals = signals;
        MeasurementBuses = measurementBuses;
        RelayStates = relayStates;
        ActiveFaults = activeFaults ?? Array.Empty<string>();
        ExternalDeviceState = externalDeviceState ?? ExternalDeviceStateSnapshot.Empty;
        ElementStates = elementStates ?? Array.Empty<string>();
        ActiveConcurrentGroup = activeConcurrentGroup;
        ConcurrentEvent = concurrentEvent;
        ConcurrentBranches = concurrentBranches ?? Array.Empty<ConcurrentBranchSnapshot>();
    }

    public string? CurrentStep { get; }
    public long CurrentTimeMs { get; }
    public IReadOnlyDictionary<string, string> Signals { get; }
    public IReadOnlyDictionary<string, string> MeasurementBuses { get; }
    public IReadOnlyList<string> RelayStates { get; }
    public IReadOnlyList<string> ActiveFaults { get; }
    public ExternalDeviceStateSnapshot ExternalDeviceState { get; }
    public IReadOnlyList<string> ElementStates { get; }
    public string? ActiveConcurrentGroup { get; }
    public string? ConcurrentEvent { get; }
    public IReadOnlyList<ConcurrentBranchSnapshot> ConcurrentBranches { get; }
}
