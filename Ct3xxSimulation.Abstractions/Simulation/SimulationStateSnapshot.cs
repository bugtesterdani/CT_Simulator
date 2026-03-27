using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Captures the complete simulator state that is exposed to the UI, export and debugging layers.
/// </summary>
public sealed class SimulationStateSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationStateSnapshot"/> class.
    /// </summary>
    /// <param name="currentStep">The current logical step name, if one is active.</param>
    /// <param name="currentTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="signals">The logical signal values visible to the simulator.</param>
    /// <param name="measurementBuses">The measurement-bus values visible to the simulator.</param>
    /// <param name="relayStates">The formatted relay states.</param>
    /// <param name="activeFaults">The currently active faults.</param>
    /// <param name="externalDeviceState">The external device state snapshot.</param>
    /// <param name="elementStates">The formatted states of simulated elements.</param>
    /// <param name="activeConcurrentGroup">The currently active concurrent group, if any.</param>
    /// <param name="concurrentEvent">The latest concurrent event description, if any.</param>
    /// <param name="concurrentBranches">The branch states of the active concurrent group.</param>
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

    /// <summary>
    /// Gets the currently active step name.
    /// </summary>
    public string? CurrentStep { get; }

    /// <summary>
    /// Gets the current simulated time in milliseconds.
    /// </summary>
    public long CurrentTimeMs { get; }

    /// <summary>
    /// Gets the logical signal values visible to the simulator.
    /// </summary>
    public IReadOnlyDictionary<string, string> Signals { get; }

    /// <summary>
    /// Gets the measurement-bus values visible to the simulator.
    /// </summary>
    public IReadOnlyDictionary<string, string> MeasurementBuses { get; }

    /// <summary>
    /// Gets the formatted relay state list.
    /// </summary>
    public IReadOnlyList<string> RelayStates { get; }

    /// <summary>
    /// Gets the currently active faults.
    /// </summary>
    public IReadOnlyList<string> ActiveFaults { get; }

    /// <summary>
    /// Gets the external device state snapshot.
    /// </summary>
    public ExternalDeviceStateSnapshot ExternalDeviceState { get; }

    /// <summary>
    /// Gets the formatted states of simulated elements.
    /// </summary>
    public IReadOnlyList<string> ElementStates { get; }

    /// <summary>
    /// Gets the currently active concurrent group, if one exists.
    /// </summary>
    public string? ActiveConcurrentGroup { get; }

    /// <summary>
    /// Gets the latest concurrent event text, if one exists.
    /// </summary>
    public string? ConcurrentEvent { get; }

    /// <summary>
    /// Gets the branch states of the active concurrent group.
    /// </summary>
    public IReadOnlyList<ConcurrentBranchSnapshot> ConcurrentBranches { get; }
}
