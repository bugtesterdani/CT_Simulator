using System;

namespace Ct3xxSimulator.Simulation;

public sealed class ConcurrentBranchSnapshot
{
    public ConcurrentBranchSnapshot(
        string branchName,
        string? currentItem,
        string status,
        long? waitUntilTimeMs = null,
        string? details = null)
    {
        BranchName = branchName ?? throw new ArgumentNullException(nameof(branchName));
        CurrentItem = currentItem;
        Status = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
        WaitUntilTimeMs = waitUntilTimeMs;
        Details = details;
    }

    public string BranchName { get; }
    public string? CurrentItem { get; }
    public string Status { get; }
    public long? WaitUntilTimeMs { get; }
    public string? Details { get; }
}
