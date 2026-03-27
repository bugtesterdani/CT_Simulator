// Provides Wire Viz Harness Resolver Building for the module runtime simulation support.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulationModelParser.Parsing;
using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed partial class WireVizHarnessResolver
{
    private static WireVizGraph BuildGraph(
        WireVizDocument document,
        SimulationModelDocument? simulationModel,
        string prefix,
        HashSet<string> visitedModels)
    {
        var connectorPins = ParseConnectorPins(document.ConnectorDefinitions, prefix);
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var prefixedSimulationElements = new List<SimulationElementDefinition>();

        foreach (var connection in document.Connections)
        {
            var segments = connection.AsSequenceOrEmpty()
                .Select(ParseConnectionSegment)
                .Where(segment => segment != null)
                .Cast<ConnectionSegment>()
                .ToList();

            if (segments.Count < 2)
            {
                continue;
            }

            var width = segments.Max(segment => segment.Terminals.Count);
            for (var index = 0; index < width; index++)
            {
                var path = new List<string>();
                foreach (var segment in segments)
                {
                    var terminal = segment.GetTerminal(index);
                    if (terminal != null)
                    {
                        path.Add($"{prefix}{segment.Designator}.{terminal}");
                    }
                }

                ConnectPath(edges, path);
            }
        }

        if (simulationModel?.Elements != null)
        {
            foreach (var element in simulationModel.Elements)
            {
                if (element is AssemblyElementDefinition assembly)
                {
                    MergeAssembly(document, assembly, prefix, connectorPins, edges, prefixedSimulationElements, visitedModels);
                }
                else
                {
                    prefixedSimulationElements.Add(PrefixElement(element, prefix));
                }
            }
        }

        return new WireVizGraph(
            document.SourcePath ?? string.Empty,
            connectorPins,
            edges,
            prefixedSimulationElements);
    }

    private static Dictionary<string, List<WireVizEndpoint>> ParseConnectorPins(IReadOnlyDictionary<string, WireVizConnectorDefinition> connectors, string prefix)
    {
        var result = new Dictionary<string, List<WireVizEndpoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var connector in connectors)
        {
            var pins = ExpandPins(connector.Value.Value);
            var labels = ExpandPinLabels(connector.Value.Value);
            var endpoints = new List<WireVizEndpoint>();
            for (var i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var label = i < labels.Count ? labels[i] : null;
                endpoints.Add(new WireVizEndpoint(
                    $"{prefix}{connector.Key}",
                    pin,
                    label,
                    connector.Value.Role,
                    connector.Value.BackgroundColor));
            }

            result[$"{prefix}{connector.Key}"] = endpoints;
        }

        return result;
    }

    private static void MergeAssembly(
        WireVizDocument parentDocument,
        AssemblyElementDefinition assembly,
        string prefix,
        Dictionary<string, List<WireVizEndpoint>> connectorPins,
        Dictionary<string, HashSet<string>> edges,
        List<SimulationElementDefinition> prefixedSimulationElements,
        HashSet<string> visitedModels)
    {
        var basePath = Path.GetDirectoryName(parentDocument.SourcePath ?? string.Empty) ?? Directory.GetCurrentDirectory();
        var childWiringPath = ResolveRelativePath(basePath, assembly.Wiring);
        if (!File.Exists(childWiringPath))
        {
            return;
        }

        var childSimulationDocument = LoadAssemblySimulationDocument(basePath, assembly.Simulation, visitedModels);
        var childKey = $"{childWiringPath}|{childSimulationDocument?.SourcePath}";
        if (!visitedModels.Add(childKey))
        {
            return;
        }

        var parser = new WireVizParser();
        var childDocument = parser.ParseFile(childWiringPath);
        var childPrefix = $"{prefix}{assembly.Id}.";
        var childGraph = BuildGraph(childDocument, childSimulationDocument, childPrefix, visitedModels);

        foreach (var pair in childGraph.Connectors)
        {
            connectorPins[pair.Key] = pair.Value;
        }

        foreach (var pair in childGraph.Edges)
        {
            foreach (var target in pair.Value)
            {
                AddEdge(edges, pair.Key, target);
            }
        }

        prefixedSimulationElements.AddRange(childGraph.SimulationElements);

        foreach (var port in assembly.Ports)
        {
            var externalKey = $"{prefix}{assembly.Id}.{port.Key}";
            var internalKey = $"{childPrefix}{port.Value}";
            AddEdge(edges, externalKey, internalKey);
            AddEdge(edges, internalKey, externalKey);
        }

        visitedModels.Remove(childKey);
    }

    private static SimulationModelDocument? LoadAssemblySimulationDocument(string basePath, string? simulationPath, HashSet<string> visitedModels)
    {
        if (string.IsNullOrWhiteSpace(simulationPath))
        {
            return null;
        }

        var fullPath = ResolveRelativePath(basePath, simulationPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var parser = new SimulationModelParser();
        return parser.ParseFile(fullPath);
    }

    private static string ResolveRelativePath(string basePath, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
    }

    private static SimulationElementDefinition PrefixElement(SimulationElementDefinition element, string prefix)
    {
        switch (element)
        {
            case RelayElementDefinition relay:
                return new RelayElementDefinition(
                    $"{prefix}{relay.Id}",
                    new RelayCoilDefinition(relay.Coil.Signal, relay.Coil.ThresholdV),
                    relay.Contacts.Select(contact => new RelayContactDefinition(
                        PrefixNode(contact.A, prefix),
                        PrefixNode(contact.B, prefix),
                        contact.Mode)).ToList(),
                    relay.Metadata);
            case ResistorElementDefinition resistor:
                return new ResistorElementDefinition(
                    $"{prefix}{resistor.Id}",
                    PrefixNode(resistor.A, prefix),
                    PrefixNode(resistor.B, prefix),
                    resistor.Ohms,
                    resistor.Metadata);
            case TransformerElementDefinition transformer:
                return new TransformerElementDefinition(
                    $"{prefix}{transformer.Id}",
                    PrefixNode(transformer.PrimaryA, prefix),
                    PrefixNode(transformer.PrimaryB, prefix),
                    PrefixNode(transformer.SecondaryA, prefix),
                    PrefixNode(transformer.SecondaryB, prefix),
                    transformer.Ratio,
                    transformer.Metadata);
            case CurrentTransformerElementDefinition currentTransformer:
                return new CurrentTransformerElementDefinition(
                    $"{prefix}{currentTransformer.Id}",
                    currentTransformer.PrimarySignal,
                    PrefixNode(currentTransformer.SecondaryA, prefix),
                    PrefixNode(currentTransformer.SecondaryB, prefix),
                    currentTransformer.Ratio,
                    currentTransformer.Metadata);
            case AssemblyElementDefinition assembly:
                return new AssemblyElementDefinition(
                    $"{prefix}{assembly.Id}",
                    assembly.Wiring,
                    assembly.Simulation,
                    assembly.Ports,
                    assembly.Metadata);
            default:
                return element;
        }
    }

    private static string PrefixNode(string node, string prefix)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return node;
        }

        return node.Contains(".", StringComparison.Ordinal)
            ? $"{prefix}{node}"
            : node;
    }

    private static List<string> ExpandPins(WireVizValue connector)
    {
        if (connector.TryGetProperty("pins", out var pins))
        {
            var explicitPins = ExpandTerminalValue(pins);
            if (explicitPins.Count > 0)
            {
                return explicitPins;
            }
        }

        var pinCount = 0;
        if (connector.TryGetProperty("pincount", out var pinCountValue))
        {
            int.TryParse(pinCountValue.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pinCount);
        }

        if (pinCount <= 0 && connector.TryGetProperty("pinlabels", out var labels))
        {
            pinCount = labels.AsSequenceOrEmpty().Count;
        }

        return pinCount <= 0
            ? new List<string>()
            : Enumerable.Range(1, pinCount).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToList();
    }

    private static List<string?> ExpandPinLabels(WireVizValue connector)
    {
        if (!connector.TryGetProperty("pinlabels", out var labels))
        {
            return new List<string?>();
        }

        return labels.AsSequenceOrEmpty()
            .Select(item => item.AsString())
            .ToList();
    }

    private static ConnectionSegment? ParseConnectionSegment(WireVizValue value)
    {
        var properties = value.AsMappingOrEmpty();
        if (properties.Count != 1)
        {
            return null;
        }

        var pair = properties.First();
        var terminals = ExpandTerminalValue(pair.Value);
        return terminals.Count == 0
            ? null
            : new ConnectionSegment(pair.Key, terminals);
    }

    private static List<string> ExpandTerminalValue(WireVizValue value)
    {
        if (value.Kind == WireVizValueKind.Sequence)
        {
            return value.Items
                .SelectMany(item => ExpandTerminalValue(item))
                .ToList();
        }

        var text = value.AsString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return ExpandToken(text);
    }

    private static List<string> ExpandToken(string token)
    {
        var trimmed = token.Trim();
        var dashIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= trimmed.Length - 1)
        {
            return new List<string> { trimmed };
        }

        var startText = trimmed[..dashIndex];
        var endText = trimmed[(dashIndex + 1)..];
        if (!int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !int.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            return new List<string> { trimmed };
        }

        var step = start <= end ? 1 : -1;
        var values = new List<string>();
        for (var current = start; current != end + step; current += step)
        {
            values.Add(current.ToString(CultureInfo.InvariantCulture));
        }

        return values;
    }

    private static void ConnectPath(Dictionary<string, HashSet<string>> edges, IReadOnlyList<string> nodes)
    {
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            AddEdge(edges, nodes[i], nodes[i + 1]);
            AddEdge(edges, nodes[i + 1], nodes[i]);
        }
    }

    private static void AddEdge(Dictionary<string, HashSet<string>> edges, string from, string to)
    {
        if (!edges.TryGetValue(from, out var targets))
        {
            targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            edges[from] = targets;
        }

        targets.Add(to);
    }

    private sealed class ConnectionSegment
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionSegment"/> class.
        /// </summary>
        public ConnectionSegment(string designator, List<string> terminals)
        {
            Designator = designator;
            Terminals = terminals;
        }

        /// <summary>
        /// Gets the designator.
        /// </summary>
        public string Designator { get; }
        /// <summary>
        /// Gets the terminals.
        /// </summary>
        public List<string> Terminals { get; }

        /// <summary>
        /// Gets the terminal.
        /// </summary>
        public string? GetTerminal(int index)
        {
            if (Terminals.Count == 0)
            {
                return null;
            }

            return index < Terminals.Count ? Terminals[index] : null;
        }
    }

}
