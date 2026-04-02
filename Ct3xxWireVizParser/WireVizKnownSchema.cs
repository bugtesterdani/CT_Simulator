using Ct3xxWireVizSchema;

namespace Ct3xxWireVizParser;

/// <summary>
/// Exposes the shared WireViz schema to parser consumers.
/// </summary>
public static class WireVizKnownSchema
{
    /// <summary>
    /// Reference commit hash for the syntax definition.
    /// </summary>
    public const string ReferenceCommit = WireVizSchema.ReferenceCommit;

    /// <summary>
    /// Known connector keys per syntax definition.
    /// </summary>
    public static IReadOnlyList<string> ConnectorKeys => WireVizSchema.ConnectorKeys;

    /// <summary>
    /// Known cable keys per syntax definition.
    /// </summary>
    public static IReadOnlyList<string> CableKeys => WireVizSchema.CableKeys;

    /// <summary>
    /// Known property type hints.
    /// </summary>
    public static IReadOnlyDictionary<string, string> PropertyTypeHints => WireVizSchema.PropertyTypeHints;
}
