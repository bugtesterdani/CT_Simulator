namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Represents a connection between two ports.
/// </summary>
public sealed class BlockEdge
{
    /// <summary>
    /// Gets or sets the edge id.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the source node id.
    /// </summary>
    public string FromNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source port id.
    /// </summary>
    public string FromPortId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target node id.
    /// </summary>
    public string ToNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target port id.
    /// </summary>
    public string ToPortId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets edge tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);
}
