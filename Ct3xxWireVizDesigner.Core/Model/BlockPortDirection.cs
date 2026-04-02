using System.Text.Json.Serialization;

namespace Ct3xxWireVizDesigner.Core.Model;

/// <summary>
/// Indicates the logical direction of a port.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BlockPortDirection
{
    In,
    Out,
    InOut
}
