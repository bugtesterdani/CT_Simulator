// Provides Breakpoint Upgrade Item for the desktop application view model support.
using System.Collections.Generic;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the breakpoint upgrade item.
/// </summary>
public sealed class BreakpointUpgradeItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BreakpointUpgradeItem"/> class.
    /// </summary>
    public BreakpointUpgradeItem(string originalKey, IReadOnlyList<BreakpointUpgradeTarget> availableTargets)
    {
        OriginalKey = originalKey;
        AvailableTargets = availableTargets;
    }

    /// <summary>
    /// Gets the original key.
    /// </summary>
    public string OriginalKey { get; }
    /// <summary>
    /// Gets the available targets.
    /// </summary>
    public IReadOnlyList<BreakpointUpgradeTarget> AvailableTargets { get; }
    /// <summary>
    /// Gets the selected target.
    /// </summary>
    public BreakpointUpgradeTarget? SelectedTarget { get; set; }
}
