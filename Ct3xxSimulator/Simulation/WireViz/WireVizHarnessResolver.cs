using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Programs;
using Ct3xxProgramParser.SignalTables;
using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizHarnessResolver
{
    private readonly Dictionary<string, List<WireVizConnectionResolution>> _resolutions;

    private WireVizHarnessResolver(Dictionary<string, List<WireVizConnectionResolution>> resolutions)
    {
        _resolutions = resolutions;
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
            return new WireVizHarnessResolver(new Dictionary<string, List<WireVizConnectionResolution>>(StringComparer.OrdinalIgnoreCase));
        }

        return Create(fileSet, wireVizFiles);
    }

    public static WireVizHarnessResolver Create(Ct3xxProgramFileSet fileSet, IEnumerable<string> wireVizFiles)
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
            .Select(BuildGraph)
            .ToList();

        var resolutions = new Dictionary<string, List<WireVizConnectionResolution>>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in fileSet.SignalTables.SelectMany(document => document.Table.AllAssignments))
        {
            foreach (var graph in graphs)
            {
                foreach (var source in graph.ResolveSignalSources(assignment))
                {
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

        return new WireVizHarnessResolver(resolutions);
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

    private static WireVizGraph BuildGraph(WireVizDocument document)
    {
        var connectorPins = ParseConnectorPins(document.ConnectorDefinitions);
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

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
                        path.Add($"{segment.Designator}.{terminal}");
                    }
                }

                ConnectPath(edges, path);
            }
        }

        return new WireVizGraph(
            document.SourcePath ?? string.Empty,
            connectorPins,
            edges);
    }

    private static Dictionary<string, List<WireVizEndpoint>> ParseConnectorPins(IReadOnlyDictionary<string, WireVizConnectorDefinition> connectors)
    {
        var result = new Dictionary<string, List<WireVizEndpoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var connector in connectors)
        {
            var pins = ExpandPins(connector.Value.Value);
            var labels = ExpandPinLabels(connector.Value.Value);
            var ct3xxSignals = ExpandCt3xxSignals(connector.Value.Value);
            var endpoints = new List<WireVizEndpoint>();
            for (var i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                var label = i < labels.Count ? labels[i] : null;
                var ct3xxSignal = i < ct3xxSignals.Count ? ct3xxSignals[i] : null;
                endpoints.Add(new WireVizEndpoint(
                    connector.Key,
                    pin,
                    label,
                    ct3xxSignal,
                    connector.Value.Role,
                    connector.Value.BackgroundColor));
            }

            result[connector.Key] = endpoints;
        }

        return result;
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

    private static List<string?> ExpandCt3xxSignals(WireVizValue connector)
    {
        if (connector.TryGetProperty("ct3xx_signals", out var labels))
        {
            return labels.AsSequenceOrEmpty()
                .Select(item => item.AsString())
                .ToList();
        }

        if (connector.TryGetProperty("ct3xx_signal_map", out var mapping))
        {
            var pins = ExpandPins(connector);
            var values = new List<string?>(pins.Count);
            foreach (var pin in pins)
            {
                values.Add(mapping.AsMappingOrEmpty().TryGetValue(pin, out var signal) ? signal.AsString() : null);
            }

            return values;
        }

        return new List<string?>();
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

        public WireVizGraph(
            string sourcePath,
            Dictionary<string, List<WireVizEndpoint>> connectors,
            Dictionary<string, HashSet<string>> edges)
        {
            SourcePath = sourcePath;
            _connectors = connectors;
            _edges = edges;
        }

        public string SourcePath { get; }

        public IEnumerable<WireVizEndpoint> ResolveSignalSources(SignalAssignment assignment)
        {
            var matches = new List<WireVizEndpoint>();
            var canonicalName = assignment.CanonicalName;
            var legacyName = assignment.Name;
            foreach (var connector in _connectors.Values)
            {
                foreach (var endpoint in connector)
                {
                    if (string.Equals(endpoint.Ct3xxSignal, canonicalName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.PinLabel, canonicalName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.Ct3xxSignal, legacyName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(endpoint.PinLabel, legacyName, StringComparison.OrdinalIgnoreCase) ||
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

        private IEnumerable<WireVizEndpoint> ResolveEndpointByKey(string key)
        {
            var dot = key.IndexOf('.', StringComparison.Ordinal);
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
}
