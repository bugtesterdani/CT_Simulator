using System.Text.Json.Serialization;

namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Represents a wire/block design graph.
/// </summary>
public sealed class BlockGraph
{
    /// <summary>
    /// Gets or sets the schema version for compatibility checks.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the graph-level metadata.
    /// </summary>
    public BlockGraphMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets WireViz metadata section (known keys only).
    /// </summary>
    public Dictionary<string, object?> WireVizMetadata { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets WireViz options section (known keys only).
    /// </summary>
    public Dictionary<string, object?> WireVizOptions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets WireViz tweak section (known keys only).
    /// </summary>
    public Dictionary<string, object?> WireVizTweak { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets additional BOM items (known keys only).
    /// </summary>
    public List<Dictionary<string, object?>> WireVizAdditionalBomItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the nodes in the graph.
    /// </summary>
    public List<BlockNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the edges in the graph.
    /// </summary>
    public List<BlockEdge> Edges { get; set; } = new();

    /// <summary>
    /// Gets a node by id.
    /// </summary>
    public BlockNode? GetNode(string id) =>
        Nodes.FirstOrDefault(node => string.Equals(node.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Gets a port by id.
    /// </summary>
    public BlockPort? GetPort(string nodeId, string portId)
    {
        var node = GetNode(nodeId);
        return node?.Ports.FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.Ordinal));
    }
}
