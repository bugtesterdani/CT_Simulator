namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Represents a connectable port on a node.
/// </summary>
public sealed class BlockPort
{
    /// <summary>
    /// Gets or sets the unique port id.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index for ordered pin/wire lists.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Gets or sets the port direction.
    /// </summary>
    public BlockPortDirection Direction { get; set; } = BlockPortDirection.InOut;

    /// <summary>
    /// Gets or sets the role hint for the port (tester, harness, device, etc.).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets optional tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);
}
