using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulationModelParser.Parsing;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Programs;
using Ct3xxProgramParser.SignalTables;
using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizHarnessResolver
{
    private readonly Dictionary<string, List<WireVizConnectionResolution>> _resolutions;
    private readonly Dictionary<string, List<ResolutionSeed>> _resolutionSeeds;
    private readonly List<SimulationElementDefinition> _simulationElements;

    private WireVizHarnessResolver(
        Dictionary<string, List<WireVizConnectionResolution>> resolutions,
        Dictionary<string, List<ResolutionSeed>> resolutionSeeds,
        List<SimulationElementDefinition>? simulationElements = null)
    {
        _resolutions = resolutions;
        _resolutionSeeds = resolutionSeeds;
        _simulationElements = simulationElements ?? new List<SimulationElementDefinition>();
    }

    public int SignalCount => _resolutions.Count;

    public static WireVizHarnessResolver Create(Ct3xxProgramFileSet fileSet)
    {
        if (fileSet == null)
        {
            throw new ArgumentNullException(nameof(fileSet));
        }

        var wireVizFiles = WireVizFileLocator.FindCandidateFiles(fileSet.ProgramDirectory);
        if (wireVizFiles.Count == 0)
        {
            return new WireVizHarnessResolver(
                new Dictionary<string, List<WireVizConnectionResolution>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, List<ResolutionSeed>>(StringComparer.OrdinalIgnoreCase));
        }

        var simulationModel = LoadSimulationModel(fileSet.ProgramDirectory);
        return Create(fileSet, wireVizFiles, simulationModel);
    }

    public static WireVizHarnessResolver Create(Ct3xxProgramFileSet fileSet, IEnumerable<string> wireVizFiles, SimulationModelDocument? simulationModel = null)
    {
        if (fileSet == null)
        {
            throw new ArgumentNullException(nameof(fileSet));
        }

        if (wireVizFiles == null)
        {
            throw new ArgumentNullException(nameof(wireVizFiles));
        }

        var parser = new WireVizParser();
        var graphs = wireVizFiles
            .Select(parser.ParseFile)
            .Select(document => BuildGraph(document, simulationModel, string.Empty, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var resolutions = new Dictionary<string, List<WireVizConnectionResolution>>(StringComparer.OrdinalIgnoreCase);
        var resolutionSeeds = new Dictionary<string, List<ResolutionSeed>>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in fileSet.SignalTables.SelectMany(document => document.Table.AllAssignments))
        {
            foreach (var graph in graphs)
            {
                foreach (var source in graph.ResolveSignalSources(assignment))
                {
                    if (!resolutionSeeds.TryGetValue(assignment.Name, out var seeds))
                    {
                        seeds = new List<ResolutionSeed>();
                        resolutionSeeds[assignment.Name] = seeds;
                    }

                    seeds.Add(new ResolutionSeed(assignment, source, graph));

                    var targets = graph.GetConnectedEndpoints(source)
                        .Where(target => !string.Equals(target.Key, source.Key, StringComparison.OrdinalIgnoreCase))
                        .DistinctBy(target => target.Key, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (targets.Count == 0)
                    {
                        continue;
                    }

                    if (!resolutions.TryGetValue(assignment.Name, out var items))
                    {
                        items = new List<WireVizConnectionResolution>();
                        resolutions[assignment.Name] = items;
                    }

                    items.Add(new WireVizConnectionResolution(assignment, source, targets, graph.SourcePath));
                }
            }
        }

        return new WireVizHarnessResolver(resolutions, resolutionSeeds, simulationModel?.Elements?.ToList());
    }

    public bool TryResolve(string signalOrTestpoint, out IReadOnlyList<WireVizConnectionResolution> resolutions)
    {
        if (!string.IsNullOrWhiteSpace(signalOrTestpoint) &&
            _resolutions.TryGetValue(signalOrTestpoint.Trim(), out var items))
        {
            resolutions = items;
            return true;
        }

        resolutions = Array.Empty<WireVizConnectionResolution>();
        return false;
    }

    public bool TryResolve(string signalOrTestpoint, IReadOnlyDictionary<string, object?> signalState, out IReadOnlyList<WireVizConnectionResolution> resolutions)
    {
        if (string.IsNullOrWhiteSpace(signalOrTestpoint))
        {
            resolutions = Array.Empty<WireVizConnectionResolution>();
            return false;
        }

        if (!_resolutionSeeds.TryGetValue(signalOrTestpoint.Trim(), out var seeds))
        {
            resolutions = Array.Empty<WireVizConnectionResolution>();
            return false;
        }

        if (_simulationElements.Count == 0)
        {
            resolutions = _resolutions.TryGetValue(signalOrTestpoint.Trim(), out var items)
                ? items
                : Array.Empty<WireVizConnectionResolution>();
            return true;
        }

        resolutions = seeds
            .Select(seed => new WireVizConnectionResolution(
                seed.Assignment,
                seed.Source,
                seed.Graph.GetConnectedEndpoints(seed.Source, signalState)
                    .Where(target => !string.Equals(target.Key, seed.Source.Key, StringComparison.OrdinalIgnoreCase))
                    .DistinctBy(target => target.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                seed.Graph.SourcePath))
            .Where(item => item.Targets.Count > 0)
            .ToList();
        return resolutions.Count > 0;
    }

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
        public ConnectionSegment(string designator, List<string> terminals)
        {
            Designator = designator;
            Terminals = terminals;
        }

        public string Designator { get; }
        public List<string> Terminals { get; }

        public string? GetTerminal(int index)
        {
            if (Terminals.Count == 0)
            {
                return null;
            }

            return index < Terminals.Count ? Terminals[index] : null;
        }
    }

    private sealed class WireVizGraph
    {
        private readonly Dictionary<string, List<WireVizEndpoint>> _connectors;
        private readonly Dictionary<string, HashSet<string>> _edges;
        private readonly IReadOnlyList<SimulationElementDefinition> _simulationElements;

        public WireVizGraph(
            string sourcePath,
            Dictionary<string, List<WireVizEndpoint>> connectors,
            Dictionary<string, HashSet<string>> edges,
            IReadOnlyList<SimulationElementDefinition> simulationElements)
        {
            SourcePath = sourcePath;
            _connectors = connectors;
            _edges = edges;
            _simulationElements = simulationElements;
        }

        public string SourcePath { get; }
        public IReadOnlyDictionary<string, List<WireVizEndpoint>> Connectors => _connectors;
        public IReadOnlyDictionary<string, HashSet<string>> Edges => _edges;
        public IReadOnlyList<SimulationElementDefinition> SimulationElements => _simulationElements;

        public IEnumerable<WireVizEndpoint> ResolveSignalSources(SignalAssignment assignment)
        {
            var matches = new List<WireVizEndpoint>();
            var canonicalName = assignment.CanonicalName;
            var legacyName = assignment.Name;
            foreach (var connector in _connectors.Values)
            {
                foreach (var endpoint in connector)
                {
                    if (string.Equals(endpoint.PinLabel, canonicalName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.PinLabel, legacyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.Pin, canonicalName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.Pin, legacyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.Key, canonicalName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.Key, legacyName, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(endpoint);
                    }
                }
            }

            if (matches.Count == 0)
            {
                yield break;
            }

            var preferred = matches
                .Where(endpoint => endpoint.Role == WireVizConnectorRole.TestSystem)
                .ToList();

            foreach (var endpoint in preferred.Count > 0 ? preferred : matches)
            {
                yield return endpoint;
            }
        }

        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(WireVizEndpoint source)
        {
            return GetConnectedEndpoints(source, null);
        }

        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(WireVizEndpoint source, IReadOnlyDictionary<string, object?>? signalState)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(source.Key);
            visited.Add(source.Key);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!_edges.TryGetValue(current, out var next))
                {
                    continue;
                }

                foreach (var target in next)
                {
                    if (visited.Add(target))
                    {
                        queue.Enqueue(target);
                    }
                }

                foreach (var target in GetDynamicTargets(current, signalState))
                {
                    if (visited.Add(target))
                    {
                        queue.Enqueue(target);
                    }
                }
            }

            var resolved = visited
                .SelectMany(key => ResolveEndpointByKey(key))
                .ToList();

            var preferredTargets = resolved
                .Where(endpoint =>
                    !string.Equals(endpoint.Key, source.Key, StringComparison.OrdinalIgnoreCase) &&
                    endpoint.Role is WireVizConnectorRole.Harness or WireVizConnectorRole.Device)
                .ToList();

            return preferredTargets.Count > 0
                ? preferredTargets
                : resolved;
        }

        private IEnumerable<string> GetDynamicTargets(string current, IReadOnlyDictionary<string, object?>? signalState)
        {
            foreach (var element in _simulationElements)
            {
                switch (element)
                {
                    case ResistorElementDefinition resistor:
                        foreach (var edge in YieldBidirectional(current, resistor.A, resistor.B))
                        {
                            yield return edge;
                        }
                        break;
                    case RelayElementDefinition relay when IsRelayClosed(relay, signalState):
                        foreach (var contact in relay.Contacts)
                        {
                            foreach (var edge in YieldBidirectional(current, contact.A, contact.B))
                            {
                                yield return edge;
                            }
                        }
                        break;
                }
            }
        }

        private static IEnumerable<string> YieldBidirectional(string current, string a, string b)
        {
            if (string.Equals(current, a, StringComparison.OrdinalIgnoreCase))
            {
                yield return b;
            }
            else if (string.Equals(current, b, StringComparison.OrdinalIgnoreCase))
            {
                yield return a;
            }
        }

        private static bool IsRelayClosed(RelayElementDefinition relay, IReadOnlyDictionary<string, object?>? signalState)
        {
            var control = signalState != null && signalState.TryGetValue(relay.Coil.Signal, out var value)
                ? value
                : null;
            var numeric = control switch
            {
                null => 0d,
                bool b => b ? relay.Coil.ThresholdV : 0d,
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0d
            };

            return numeric >= relay.Coil.ThresholdV;
        }

        private IEnumerable<WireVizEndpoint> ResolveEndpointByKey(string key)
        {
            var dot = key.LastIndexOf('.');
            if (dot <= 0 || dot >= key.Length - 1)
            {
                yield break;
            }

            var designator = key[..dot];
            var pin = key[(dot + 1)..];
            if (!_connectors.TryGetValue(designator, out var endpoints))
            {
                yield break;
            }

            foreach (var endpoint in endpoints)
            {
                if (string.Equals(endpoint.Pin, pin, StringComparison.OrdinalIgnoreCase))
                {
                    yield return endpoint;
                }
            }
        }
    }

    private static SimulationModelDocument? LoadSimulationModel(string programDirectory)
    {
        var path = SimulationModelFileLocator.FindCandidateFile(programDirectory);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parser = new SimulationModelParser();
        return parser.ParseFile(path);
    }

    private sealed class ResolutionSeed
    {
        public ResolutionSeed(SignalAssignment assignment, WireVizEndpoint source, WireVizGraph graph)
        {
            Assignment = assignment;
            Source = source;
            Graph = graph;
        }

        public SignalAssignment Assignment { get; }
        public WireVizEndpoint Source { get; }
        public WireVizGraph Graph { get; }
    }
}
