namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Represents a node in the block graph.
/// </summary>
public sealed class BlockNode
{
    /// <summary>
    /// Gets or sets the node id.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the node name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node type.
    /// </summary>
    public BlockNodeType Type { get; set; } = BlockNodeType.Connector;

    /// <summary>
    /// Gets or sets the ports.
    /// </summary>
    public List<BlockPort> Ports { get; set; } = new();

    /// <summary>
    /// Gets or sets the position X for UI layout.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the position Y for UI layout.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets node-level tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets known WireViz properties for this node.
    /// </summary>
    public Dictionary<string, object?> WireVizProps { get; set; } = new(StringComparer.Ordinal);
}
