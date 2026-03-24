using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.SignalTables;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulator.Simulation.FaultInjection;
using Ct3xxWireVizParser.Model;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed partial class WireVizHarnessResolver
{
    private sealed partial class WireVizGraph
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
            return GetConnectedEndpoints(source, null, null, 0L, SimulationFaultSet.Empty);
        }

        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(WireVizEndpoint source, IReadOnlyDictionary<string, object?>? signalState)
        {
            return GetConnectedEndpoints(source, signalState, null, 0L, SimulationFaultSet.Empty);
        }

        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(
            WireVizEndpoint source,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults)
        {
            var traversal = Traverse(source.Key, signalState, signalTimes, currentTimeMs, faults);
            var resolved = traversal.Visited
                .SelectMany(key => ResolveEndpointByKey(key))
                .ToList();
            return SelectPreferredTargets(source, resolved);
        }

        public IReadOnlyList<WireVizSignalTrace> TraceSignalPaths(WireVizEndpoint source, IReadOnlyDictionary<string, object?>? signalState)
        {
            return TraceSignalPaths(source, signalState, null, 0L, SimulationFaultSet.Empty);
        }

        public IReadOnlyList<WireVizSignalTrace> TraceSignalPaths(
            WireVizEndpoint source,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults)
        {
            var traversal = Traverse(source.Key, signalState, signalTimes, currentTimeMs, faults);
            var resolved = traversal.Visited
                .SelectMany(key => ResolveEndpointByKey(key))
                .ToList();

            var targets = SelectPreferredTargets(source, resolved)
                .DistinctBy(endpoint => endpoint.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (targets.Count == 0)
            {
                return Array.Empty<WireVizSignalTrace>();
            }

            var traces = new List<WireVizSignalTrace>();
            foreach (var target in targets)
            {
                if (!TryReconstructPath(source.Key, target.Key, traversal.Parents, out var path))
                {
                    continue;
                }

                traces.Add(new WireVizSignalTrace(
                    $"{source.DisplayName} -> {target.DisplayName}",
                    path.Select(FormatNode).ToList()));
            }

            return traces;
        }

        public IReadOnlyList<WireVizRuntimeTarget> ResolveRuntimeTargets(
            WireVizEndpoint source,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults,
            bool forWrite)
        {
            var traversal = Traverse(source.Key, signalState, signalTimes, currentTimeMs, faults, forWrite);
            var resolvedEndpoints = traversal.Visited
                .SelectMany(key => ResolveEndpointByKey(key))
                .ToList();

            var selectedEndpoints = SelectPreferredTargets(source, resolvedEndpoints)
                .DistinctBy(endpoint => endpoint.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<WireVizRuntimeTarget>();
            foreach (var synthetic in traversal.SyntheticTargets)
            {
                if (forWrite && synthetic.WriteOnly == false)
                {
                    continue;
                }

                if (!forWrite && synthetic.ReadOnly == false)
                {
                    continue;
                }

                result.Add(new WireVizRuntimeTarget(
                    synthetic.SignalName,
                    synthetic.SourceToTargetScale,
                    transformDescription: synthetic.Description,
                    synthetic: true));
            }

            foreach (var endpoint in selectedEndpoints)
            {
                if (string.Equals(endpoint.Key, source.Key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var signalName = string.IsNullOrWhiteSpace(endpoint.PinLabel) ? endpoint.Key : endpoint.PinLabel!;
                var scale = traversal.SourceToNodeScale.TryGetValue(endpoint.Key, out var endpointScale)
                    ? endpointScale
                    : 1d;
                var transform = !string.Equals(source.Key, endpoint.Key, StringComparison.OrdinalIgnoreCase) &&
                                Math.Abs(scale - 1d) > 0.0000001d
                    ? $"scale {scale.ToString("0.###", CultureInfo.InvariantCulture)}"
                    : null;
                result.Add(new WireVizRuntimeTarget(signalName, scale, endpoint, transform));
            }

            return result
                .GroupBy(item => item.SignalName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private TraversalResult Traverse(
            string sourceKey,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults,
            bool forWrite = false)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scales = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceKey] = 1d
            };
            var syntheticTargets = new Dictionary<string, SyntheticRuntimeTarget>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(sourceKey);
            visited.Add(sourceKey);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentScale = scales.TryGetValue(current, out var scale) ? scale : 1d;
                foreach (var edge in EnumerateAdjacentNodes(current, signalState, signalTimes, currentTimeMs, faults, forWrite))
                {
                    if (edge.IsSyntheticSignal)
                    {
                        if (!syntheticTargets.ContainsKey(edge.Target))
                        {
                            syntheticTargets[edge.Target] = new SyntheticRuntimeTarget(
                                edge.Target,
                                currentScale * edge.Scale,
                                edge.Description,
                                edge.WriteOnly,
                                edge.ReadOnly);
                        }

                        continue;
                    }

                    if (!visited.Add(edge.Target))
                    {
                        continue;
                    }

                    parents[edge.Target] = current;
                    scales[edge.Target] = currentScale * edge.Scale;
                    queue.Enqueue(edge.Target);
                }
            }

            return new TraversalResult(visited, parents, scales, syntheticTargets.Values.ToList());
        }

        private IEnumerable<TraversalEdge> EnumerateAdjacentNodes(
            string current,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults,
            bool forWrite)
        {
            if (_edges.TryGetValue(current, out var next))
            {
                foreach (var target in next)
                {
                    if (faults.IsOpenConnection(current, target) || faults.HasContactProblem(current, target, currentTimeMs))
                    {
                        continue;
                    }

                    yield return TraversalEdge.Node(target);
                }
            }

            foreach (var shorted in faults.Faults.Where(fault =>
                         fault.Enabled &&
                         string.Equals(fault.Type, "short_connection", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(shorted.A, current, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(shorted.B))
                {
                    yield return TraversalEdge.Node(shorted.B!);
                }
                else if (string.Equals(shorted.B, current, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(shorted.A))
                {
                    yield return TraversalEdge.Node(shorted.A!);
                }
            }

            foreach (var target in GetDynamicTargets(current, signalState, signalTimes, currentTimeMs, faults, forWrite))
            {
                yield return target;
            }
        }

        private IReadOnlyList<WireVizEndpoint> SelectPreferredTargets(WireVizEndpoint source, IReadOnlyList<WireVizEndpoint> resolved)
        {
            var preferredTargets = resolved
                .Where(endpoint =>
                    !string.Equals(endpoint.Key, source.Key, StringComparison.OrdinalIgnoreCase) &&
                    endpoint.Role is WireVizConnectorRole.Harness or WireVizConnectorRole.Device)
                .ToList();

            return preferredTargets.Count > 0
                ? preferredTargets
                : resolved;
        }

        private static bool TryReconstructPath(
            string sourceKey,
            string targetKey,
            IReadOnlyDictionary<string, string> parents,
            out List<string> path)
        {
            path = new List<string>();
            if (string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                path.Add(sourceKey);
                return true;
            }

            if (!parents.ContainsKey(targetKey))
            {
                return false;
            }

            var current = targetKey;
            path.Add(current);
            while (!string.Equals(current, sourceKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!parents.TryGetValue(current, out var parent))
                {
                    return false;
                }

                current = parent;
                path.Add(current);
            }

            path.Reverse();
            return true;
        }

        private string FormatNode(string key)
        {
            if (key.StartsWith("@signal:", StringComparison.OrdinalIgnoreCase))
            {
                return $"Signal {key["@signal:".Length..]}";
            }

            var endpoint = ResolveEndpointByKey(key).FirstOrDefault();
            return endpoint?.DisplayName ?? key;
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

        private sealed class TraversalResult
        {
            public TraversalResult(
                HashSet<string> visited,
                Dictionary<string, string> parents,
                Dictionary<string, double> sourceToNodeScale,
                IReadOnlyList<SyntheticRuntimeTarget> syntheticTargets)
            {
                Visited = visited;
                Parents = parents;
                SourceToNodeScale = sourceToNodeScale;
                SyntheticTargets = syntheticTargets;
            }

            public HashSet<string> Visited { get; }
            public Dictionary<string, string> Parents { get; }
            public Dictionary<string, double> SourceToNodeScale { get; }
            public IReadOnlyList<SyntheticRuntimeTarget> SyntheticTargets { get; }
        }

        private sealed class TraversalEdge
        {
            private TraversalEdge(string target, double scale, string? description, bool isSyntheticSignal, bool writeOnly, bool readOnly)
            {
                Target = target;
                Scale = scale;
                Description = description;
                IsSyntheticSignal = isSyntheticSignal;
                WriteOnly = writeOnly;
                ReadOnly = readOnly;
            }

            public string Target { get; }
            public double Scale { get; }
            public string? Description { get; }
            public bool IsSyntheticSignal { get; }
            public bool WriteOnly { get; }
            public bool ReadOnly { get; }

            public static TraversalEdge Node(string target, double scale = 1d, string? description = null) =>
                new(target, scale, description, false, false, false);

            public static TraversalEdge SyntheticSignal(string signalName, double scale, string? description, bool writeOnly = false, bool readOnly = false) =>
                new($"@signal:{signalName}", scale, description, true, writeOnly, readOnly);
        }

        private sealed class SyntheticRuntimeTarget
        {
            public SyntheticRuntimeTarget(string key, double sourceToTargetScale, string? description, bool writeOnly, bool readOnly)
            {
                Key = key;
                SourceToTargetScale = sourceToTargetScale;
                Description = description;
                WriteOnly = writeOnly;
                ReadOnly = readOnly;
            }

            public string Key { get; }
            public string SignalName => Key.StartsWith("@signal:", StringComparison.OrdinalIgnoreCase)
                ? Key["@signal:".Length..]
                : Key;
            public double SourceToTargetScale { get; }
            public string? Description { get; }
            public bool WriteOnly { get; }
            public bool ReadOnly { get; }
        }
    }

}
