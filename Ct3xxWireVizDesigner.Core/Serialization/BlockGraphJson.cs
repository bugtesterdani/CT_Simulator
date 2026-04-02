using System.Text.Json;
using System.Text.Json.Serialization;
using Ct3xxWireVizDesigner.Core.Model;

namespace Ct3xxWireVizDesigner.Core.Serialization;

/// <summary>
/// Serializes block graphs to JSON for the UI.
/// </summary>
public static class BlockGraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Serialize to JSON.
    /// </summary>
    public static string Serialize(BlockGraph graph) =>
        JsonSerializer.Serialize(graph, Options);

    /// <summary>
    /// Deserialize from JSON.
    /// </summary>
    public static BlockGraph Deserialize(string json) =>
        JsonSerializer.Deserialize<BlockGraph>(json, Options) ?? new BlockGraph();
}
