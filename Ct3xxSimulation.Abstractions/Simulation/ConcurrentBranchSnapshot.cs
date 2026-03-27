using System;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Describes the current state of one branch inside a concurrent group snapshot.
/// </summary>
public sealed class ConcurrentBranchSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentBranchSnapshot"/> class.
    /// </summary>
    /// <param name="branchName">The display name of the branch.</param>
    /// <param name="currentItem">The currently active item within the branch, if any.</param>
    /// <param name="status">The normalized branch status text.</param>
    /// <param name="waitUntilTimeMs">The next resume time in milliseconds when the branch is waiting.</param>
    /// <param name="details">Additional branch detail text.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="branchName"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Gets the display name of the branch.
    /// </summary>
    public string BranchName { get; }

    /// <summary>
    /// Gets the currently active item within the branch.
    /// </summary>
    public string? CurrentItem { get; }

    /// <summary>
    /// Gets the normalized branch status, for example <c>running</c> or <c>waiting</c>.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets the simulated time in milliseconds at which the branch can continue.
    /// </summary>
    public long? WaitUntilTimeMs { get; }

    /// <summary>
    /// Gets additional detail text associated with the branch state.
    /// </summary>
    public string? Details { get; }
}
