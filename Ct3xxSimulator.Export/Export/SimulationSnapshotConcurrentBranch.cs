// Provides Simulation Snapshot Concurrent Branch for the export layer export support.
namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation snapshot concurrent branch.
/// </summary>
public sealed class SimulationSnapshotConcurrentBranch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSnapshotConcurrentBranch"/> class.
    /// </summary>
    public SimulationSnapshotConcurrentBranch(
        string branchName,
        string? currentItem,
        string status,
        long? waitUntilTimeMs,
        string? details)
    {
        BranchName = branchName;
        CurrentItem = currentItem;
        Status = status;
        WaitUntilTimeMs = waitUntilTimeMs;
        Details = details;
    }

    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public string BranchName { get; }
    /// <summary>
    /// Gets the current item.
    /// </summary>
    public string? CurrentItem { get; }
    /// <summary>
    /// Gets the status.
    /// </summary>
    public string Status { get; }
    /// <summary>
    /// Gets the wait until time ms.
    /// </summary>
    public long? WaitUntilTimeMs { get; }
    /// <summary>
    /// Gets the details.
    /// </summary>
    public string? Details { get; }
}
