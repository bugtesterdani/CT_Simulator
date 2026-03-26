namespace Ct3xxSimulator.Export;

public sealed class SimulationSnapshotConcurrentBranch
{
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

    public string BranchName { get; }
    public string? CurrentItem { get; }
    public string Status { get; }
    public long? WaitUntilTimeMs { get; }
    public string? Details { get; }
}
