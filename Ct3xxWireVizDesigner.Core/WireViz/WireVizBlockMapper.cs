using Ct3xxWireVizDesigner.Core.Model;
using Ct3xxWireVizSchema;
using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;
using YamlDotNet.Serialization;

namespace Ct3xxWireVizDesigner.Core.WireViz;

/// <summary>
/// Maps WireViz YAML to the block graph model and back.
/// </summary>
public static class WireVizBlockMapper
{
    private static readonly HashSet<string> ConnectorKeys =
        new(WireVizSchema.ConnectorKeys, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> CableKeys =
        new(WireVizSchema.CableKeys, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a WireViz YAML document into a block graph.
    /// </summary>
    public static BlockGraph ImportFromYaml(string yaml, string? sourcePath = null)
    {
        var parser = new WireVizParser();
        var document = parser.Parse(yaml, sourcePath);

        var graph = new BlockGraph
        {
            Metadata =
            {
                SourceFormat = "wireviz",
                WireVizRawYaml = yaml
            }
        };

        if (document.Metadata is { } meta)
        {
            graph.WireVizMetadata = ConvertSection(meta, null);
            if (meta.TryGetProperty("title", out var titleValue))
            {
                graph.Metadata.Title = titleValue.AsString();
            }
        }

        if (document.Options is { } options)
        {
            graph.WireVizOptions = ConvertSection(options, null);
        }

        if (document.Tweak is { } tweak)
        {
            graph.WireVizTweak = ConvertSection(tweak, null);
        }

        if (document.AdditionalBomItems.Count > 0)
        {
            graph.WireVizAdditionalBomItems = document.AdditionalBomItems
                .Select(item => ConvertValue(item) as Dictionary<string, object?> ?? new Dictionary<string, object?>())
                .ToList();
        }

        var nodeIndex = new Dictionary<string, BlockNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var connector in document.ConnectorDefinitions)
        {
            var node = new BlockNode
            {
                Name = connector.Key,
                Type = MapConnectorRole(connector.Value.Role)
            };

            PopulateConnectorPorts(node, connector.Value);
            node.WireVizProps = ConvertSection(connector.Value.Value, ConnectorKeys);
            node.Tags["role"] = connector.Value.Role.ToString();
            nodeIndex[connector.Key] = node;
            graph.Nodes.Add(node);
        }

        foreach (var cable in document.Cables)
        {
            var node = new BlockNode
            {
                Name = cable.Key,
                Type = BlockNodeType.Cable
            };

            PopulateCablePorts(node, cable.Value);
            node.WireVizProps = ConvertSection(cable.Value, CableKeys);
            nodeIndex[cable.Key] = node;
            graph.Nodes.Add(node);
        }

        foreach (var bundle in document.Bundles)
        {
            var node = new BlockNode
            {
                Name = bundle.Key,
                Type = BlockNodeType.Bundle
            };

            node.Tags["raw"] = bundle.Value.ToString();
            nodeIndex[bundle.Key] = node;
            graph.Nodes.Add(node);
        }

        foreach (var connection in document.Connections)
        {
            AddEdgesFromConnection(graph, connection, nodeIndex);
        }

        return graph;
    }

    /// <summary>
    /// Serializes a block graph as WireViz YAML.
    /// </summary>
    public static string ExportToYaml(BlockGraph graph, bool full = false)
    {
        if (graph == null)
        {
            throw new ArgumentNullException(nameof(graph));
        }

        var root = new Dictionary<string, object>();
        if (graph.WireVizMetadata != null && graph.WireVizMetadata.Count > 0)
        {
            root["metadata"] = NormalizeSection(graph.WireVizMetadata);
        }
        var metadata = graph.Metadata ?? new BlockGraphMetadata();
        if (!string.IsNullOrWhiteSpace(metadata.Title))
        {
            if (!root.TryGetValue("metadata", out var metaSection) || metaSection is not Dictionary<string, object> metaDict)
            {
                metaDict = new Dictionary<string, object>();
                root["metadata"] = metaDict;
            }
            if (!metaDict.ContainsKey("title"))
            {
                metaDict["title"] = metadata.Title!;
            }
        }

        if (graph.WireVizOptions != null && graph.WireVizOptions.Count > 0)
        {
            root["options"] = NormalizeSection(graph.WireVizOptions);
        }

        if (graph.WireVizTweak != null && graph.WireVizTweak.Count > 0)
        {
            root["tweak"] = NormalizeSection(graph.WireVizTweak);
        }

        if (graph.WireVizAdditionalBomItems != null && graph.WireVizAdditionalBomItems.Count > 0)
        {
            root["additional_bom_items"] = graph.WireVizAdditionalBomItems
                .Select(item => NormalizeSection(item))
                .ToList();
        }

        var connectors = new Dictionary<string, object>();
        var cables = new Dictionary<string, object>();
        foreach (var node in graph.Nodes ?? new List<BlockNode>())
        {
            if (node.Type == BlockNodeType.Cable)
            {
                cables[node.Name] = BuildCableYaml(node, full);
                continue;
            }

            if (node.Type is BlockNodeType.Connector or BlockNodeType.Device or BlockNodeType.Harness)
            {
                connectors[node.Name] = BuildConnectorYaml(node, full);
            }
        }

        if (connectors.Count > 0)
        {
            root["connectors"] = connectors;
        }

        if (cables.Count > 0)
        {
            root["cables"] = cables;
        }

        var connections = new List<object>();
        foreach (var edge in graph.Edges ?? new List<BlockEdge>())
        {
            var fromNode = graph.GetNode(edge.FromNodeId);
            var toNode = graph.GetNode(edge.ToNodeId);
            var fromPort = graph.GetPort(edge.FromNodeId, edge.FromPortId);
            var toPort = graph.GetPort(edge.ToNodeId, edge.ToPortId);
            if (fromNode is null || toNode is null || fromPort is null || toPort is null)
            {
                continue;
            }

            connections.Add(new List<object>
            {
                new Dictionary<string, object>
                {
                    [fromNode.Name] = new List<object> { FormatPortIndex(fromPort) }
                },
                new Dictionary<string, object>
                {
                    [toNode.Name] = new List<object> { FormatPortIndex(toPort) }
                }
            });
        }

        if (connections.Count > 0)
        {
            root["connections"] = connections;
        }

        var serializer = new SerializerBuilder().Build();
        return serializer.Serialize(root);
    }

    private static void PopulateConnectorPorts(BlockNode node, WireVizConnectorDefinition definition)
    {
        var pinLabels = definition.Value.TryGetProperty("pinlabels", out var labelsValue)
            ? labelsValue.AsSequenceOrEmpty().Select(item => item.AsString()).ToList()
            : new List<string?>();

        var pins = definition.Value.TryGetProperty("pins", out var pinsValue)
            ? pinsValue.AsSequenceOrEmpty().Select(item => item.AsString()).ToList()
            : new List<string?>();

        var count = Math.Max(pinLabels.Count, pins.Count);
        for (var i = 0; i < count; i++)
        {
            var label = pinLabels.Count > i ? pinLabels[i] : null;
            var pin = pins.Count > i ? pins[i] : null;
            node.Ports.Add(new BlockPort
            {
                Name = label ?? pin ?? (i + 1).ToString(),
                Index = i + 1
            });
        }
    }

    private static void PopulateCablePorts(BlockNode node, WireVizValue cableValue)
    {
        var labels = cableValue.TryGetProperty("wirelabels", out var labelsValue)
            ? labelsValue.AsSequenceOrEmpty().Select(item => item.AsString()).ToList()
            : new List<string?>();
        var count = cableValue.TryGetProperty("wirecount", out var countValue)
            ? ParseIndex(countValue.AsString())
            : 0;
        if (count == 0)
        {
            count = Math.Max(labels.Count, 1);
        }

        for (var i = 0; i < count; i++)
        {
            var label = labels.Count > i ? labels[i] : null;
            node.Ports.Add(new BlockPort
            {
                Name = label ?? $"W{i + 1}",
                Index = i + 1
            });
        }
    }

    private static void AddEdgesFromConnection(
        BlockGraph graph,
        WireVizValue connection,
        Dictionary<string, BlockNode> nodeIndex)
    {
        var endpoints = new List<(BlockNode Node, List<int> Pins)>();
        foreach (var endpoint in connection.AsSequenceOrEmpty())
        {
            if (endpoint.Kind != WireVizValueKind.Mapping)
            {
                continue;
            }

            foreach (var pair in endpoint.Properties)
            {
                if (!nodeIndex.TryGetValue(pair.Key, out var node))
                {
                    continue;
                }

                var pins = pair.Value.AsSequenceOrEmpty()
                    .Select(item => ParseIndex(item.AsString()))
                    .Where(value => value > 0)
                    .ToList();
                if (pins.Count == 0)
                {
                    continue;
                }

                endpoints.Add((node, pins));
            }
        }

        if (endpoints.Count < 2)
        {
            return;
        }

        var primary = endpoints[0];
        for (var i = 1; i < endpoints.Count; i++)
        {
            var secondary = endpoints[i];
            var count = Math.Min(primary.Pins.Count, secondary.Pins.Count);
            for (var idx = 0; idx < count; idx++)
            {
                var fromPort = FindPort(primary.Node, primary.Pins[idx]);
                var toPort = FindPort(secondary.Node, secondary.Pins[idx]);
                if (fromPort is null || toPort is null)
                {
                    continue;
                }

                graph.Edges.Add(new BlockEdge
                {
                    FromNodeId = primary.Node.Id,
                    FromPortId = fromPort.Id,
                    ToNodeId = secondary.Node.Id,
                    ToPortId = toPort.Id
                });
            }
        }
    }

    private static BlockPort? FindPort(BlockNode node, int index) =>
        node.Ports.FirstOrDefault(port => port.Index == index);

    private static BlockNodeType MapConnectorRole(WireVizConnectorRole role) =>
        role switch
        {
            WireVizConnectorRole.Device => BlockNodeType.Device,
            WireVizConnectorRole.Harness => BlockNodeType.Harness,
            WireVizConnectorRole.TestSystem => BlockNodeType.Connector,
            _ => BlockNodeType.Connector
        };

    private static Dictionary<string, object> BuildConnectorYaml(BlockNode node, bool full)
    {
        var payload = NormalizeSection(node.WireVizProps ?? new Dictionary<string, object?>());
        var ports = node.Ports ?? new List<BlockPort>();
        var pins = ports.Select(port => port.Index?.ToString() ?? port.Name).ToList();
        var pinLabels = ports.Select(port => port.Name).ToList();
        payload["pins"] = pins;
        payload["pinlabels"] = pinLabels;
        if (node.Tags.TryGetValue("role", out var role))
        {
            payload["role"] = role;
        }

        if (full)
        {
            foreach (var key in ConnectorKeys)
            {
                payload.TryAdd(key, string.Empty);
            }
        }
        return TrimNulls(payload, full);
    }

    private static Dictionary<string, object> BuildCableYaml(BlockNode node, bool full)
    {
        var payload = NormalizeSection(node.WireVizProps ?? new Dictionary<string, object?>());
        var ports = node.Ports ?? new List<BlockPort>();
        payload["wirecount"] = ports.Count;
        payload["wirelabels"] = ports.Select(port => port.Name).ToList();
        if (full)
        {
            foreach (var key in CableKeys)
            {
                payload.TryAdd(key, string.Empty);
            }
        }
        return TrimNulls(payload, full);
    }

    private static string FormatPortIndex(BlockPort port) =>
        port.Index?.ToString() ?? port.Name;

    private static Dictionary<string, object?> ConvertSection(WireVizValue section, HashSet<string>? allowedKeys)
    {
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (section.Kind != WireVizValueKind.Mapping)
        {
            return output;
        }

        foreach (var pair in section.Properties)
        {
            output[pair.Key] = ConvertValue(pair.Value);
        }

        return output;
    }

    private static object? ConvertValue(WireVizValue value)
    {
        return value.Kind switch
        {
            WireVizValueKind.Null => null,
            WireVizValueKind.Scalar => value.Scalar,
            WireVizValueKind.Sequence => value.Items.Select(ConvertValue).ToList(),
            WireVizValueKind.Mapping => value.Properties.ToDictionary(
                pair => pair.Key,
                pair => ConvertValue(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => value.AsString()
        };
    }

    private static Dictionary<string, object> NormalizeSection(Dictionary<string, object?> source)
    {
        var output = new Dictionary<string, object>();
        foreach (var pair in source)
        {
            if (pair.Value is null)
            {
                continue;
            }
            output[pair.Key] = NormalizeValue(pair.Value);
        }
        return output;
    }

    private static Dictionary<string, object> TrimNulls(Dictionary<string, object> source, bool full)
    {
        if (full)
        {
            return source;
        }
        return source.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static object NormalizeValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement element => NormalizeJsonElement(element),
            Dictionary<string, object?> map => NormalizeSection(map),
            IEnumerable<object?> list => list.Where(item => item != null).Select(item => NormalizeValue(item!)).ToList(),
            _ => value
        };
    }

    private static object NormalizeJsonElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => NormalizeJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => string.Empty
        };
    }

    private static int ParseIndex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
