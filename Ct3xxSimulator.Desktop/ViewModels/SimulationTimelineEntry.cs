using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.ViewModels;

public sealed class SimulationTimelineEntry
{
    public SimulationTimelineEntry(int index, SimulationStateSnapshot snapshot)
    {
        Index = index;
        Snapshot = snapshot;
    }

    public int Index { get; }
    public SimulationStateSnapshot Snapshot { get; }
    public string Label => $"{Index + 1}: {Snapshot.CurrentStep ?? "-"} @ {Snapshot.CurrentTimeMs} ms";
}
