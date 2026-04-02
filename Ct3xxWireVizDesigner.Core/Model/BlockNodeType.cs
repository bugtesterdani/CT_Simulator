using System.Text.Json.Serialization;

namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Describes the semantic node category in a block graph.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BlockNodeType
{
    Connector,
    Cable,
    Bundle,
    Splice,
    Device,
    Harness,
    Comment
}
