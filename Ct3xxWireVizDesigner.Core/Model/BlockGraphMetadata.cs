namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Stores graph-level metadata and source hints.
/// </summary>
public sealed class BlockGraphMetadata
{
    /// <summary>
    /// Gets or sets a human readable title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets a descriptive summary.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the source format, e.g. "wireviz".
    /// </summary>
    public string? SourceFormat { get; set; }

    /// <summary>
    /// Gets or sets an optional raw WireViz payload for round-trip fallback.
    /// </summary>
    public string? WireVizRawYaml { get; set; }

    /// <summary>
    /// Gets or sets arbitrary metadata tags.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);
}
