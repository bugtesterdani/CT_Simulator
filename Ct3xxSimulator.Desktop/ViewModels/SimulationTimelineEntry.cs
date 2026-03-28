// Provides Simulation Timeline Entry for the desktop application view model support.
using Ct3xxSimulator.Simulation;
using System.Linq;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the simulation timeline entry.
/// </summary>
public sealed class SimulationTimelineEntry
{
    private string _resultSourceLabel = string.Empty;
    private string _comparisonLabel = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationTimelineEntry"/> class.
    /// </summary>
    public SimulationTimelineEntry(int index, SimulationStateSnapshot snapshot)
    {
        Index = index;
        Snapshot = snapshot;
    }

    /// <summary>
    /// Gets the index.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// Gets the snapshot.
    /// </summary>
    public SimulationStateSnapshot Snapshot { get; }
    /// <summary>
    /// Gets the label.
    /// </summary>
    public string Label => $"{Index + 1}: {Snapshot.CurrentStep ?? "-"} @ {Snapshot.CurrentTimeMs} ms";
    /// <summary>
    /// Gets the event label.
    /// </summary>
    public string EventLabel => string.IsNullOrWhiteSpace(Snapshot.ConcurrentEvent) ? (Snapshot.CurrentStep ?? "-") : Snapshot.ConcurrentEvent!;
    /// <summary>
    /// Gets the branch summary.
    /// </summary>
    public string BranchSummary => Snapshot.ConcurrentBranches.Count == 0
        ? "-"
        : string.Join(", ", Snapshot.ConcurrentBranches.Select(item => $"{item.BranchName}: {item.Status}"));
    /// <summary>
    /// Gets or sets the result source label associated with this snapshot.
    /// </summary>
    public string ResultSourceLabel
    {
        get => _resultSourceLabel;
        set => _resultSourceLabel = value ?? string.Empty;
    }
    /// <summary>
    /// Gets or sets the short comparison label associated with this snapshot.
    /// </summary>
    public string ComparisonLabel
    {
        get => _comparisonLabel;
        set => _comparisonLabel = value ?? string.Empty;
    }
    /// <summary>
    /// Gets a value indicating whether the snapshot carries CSV comparison information.
    /// </summary>
    public bool HasComparisonLabel => !string.IsNullOrWhiteSpace(_comparisonLabel);
}
