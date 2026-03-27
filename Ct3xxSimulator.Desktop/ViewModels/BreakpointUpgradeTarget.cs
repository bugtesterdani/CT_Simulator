// Provides Breakpoint Upgrade Target for the desktop application view model support.
namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the breakpoint upgrade target.
/// </summary>
public sealed class BreakpointUpgradeTarget
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BreakpointUpgradeTarget"/> class.
    /// </summary>
    public BreakpointUpgradeTarget(string nodeKey, string displayName)
    {
        NodeKey = nodeKey;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the node key.
    /// </summary>
    public string NodeKey { get; }
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Executes to string.
    /// </summary>
    public override string ToString() => DisplayName;
}
