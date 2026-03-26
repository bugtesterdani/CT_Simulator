using Ct3xxSimulator.Simulation;
using System.Linq;

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
    public string EventLabel => string.IsNullOrWhiteSpace(Snapshot.ConcurrentEvent) ? (Snapshot.CurrentStep ?? "-") : Snapshot.ConcurrentEvent!;
    public string BranchSummary => Snapshot.ConcurrentBranches.Count == 0
        ? "-"
        : string.Join(", ", Snapshot.ConcurrentBranches.Select(item => $"{item.BranchName}: {item.Status}"));
}
