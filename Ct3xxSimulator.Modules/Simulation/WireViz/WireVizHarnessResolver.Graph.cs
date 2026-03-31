// Provides Wire Viz Harness Resolver Graph for the module runtime simulation support.
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
        private static readonly HashSet<string> GenericBoundaryLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            "IN",
            "OUT",
            "GND",
            "VPLUS",
            "VMINUS",
            "VIN",
            "VOUT",
            "SIG",
            "INPUT",
            "OUTPUT"
        };

        private readonly Dictionary<string, List<WireVizEndpoint>> _connectors;
        private readonly Dictionary<string, HashSet<string>> _edges;
        private readonly IReadOnlyList<SimulationElementDefinition> _simulationElements;

        /// <summary>
        /// Initializes a new instance of the <see cref="WireVizGraph"/> class.
        /// </summary>
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

        /// <summary>
        /// Gets the source path.
        /// </summary>
        public string SourcePath { get; }
        /// <summary>
        /// Gets the connectors.
        /// </summary>
        public IReadOnlyDictionary<string, List<WireVizEndpoint>> Connectors => _connectors;
        /// <summary>
        /// Gets the edges.
        /// </summary>
        public IReadOnlyDictionary<string, HashSet<string>> Edges => _edges;
        /// <summary>
        /// Gets the simulation elements.
        /// </summary>
        public IReadOnlyList<SimulationElementDefinition> SimulationElements => _simulationElements;

        /// <summary>
        /// Resolves the signal sources.
        /// </summary>
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

        /// <summary>
        /// Gets the connected endpoints.
        /// </summary>
        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(WireVizEndpoint source)
        {
            return GetConnectedEndpoints(source, null, null, 0L, SimulationFaultSet.Empty);
        }

        /// <summary>
        /// Gets the connected endpoints.
        /// </summary>
        public IReadOnlyList<WireVizEndpoint> GetConnectedEndpoints(WireVizEndpoint source, IReadOnlyDictionary<string, object?>? signalState)
        {
            return GetConnectedEndpoints(source, signalState, null, 0L, SimulationFaultSet.Empty);
        }

        /// <summary>
        /// Gets the connected endpoints.
        /// </summary>
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
            return SelectPreferredTargets(source, resolved, preserveSourceSignalName: false);
        }

        /// <summary>
        /// Executes trace signal paths.
        /// </summary>
        public IReadOnlyList<WireVizSignalTrace> TraceSignalPaths(WireVizEndpoint source, IReadOnlyDictionary<string, object?>? signalState)
        {
            return TraceSignalPaths(source, signalState, null, 0L, SimulationFaultSet.Empty);
        }

        /// <summary>
        /// Executes trace signal paths.
        /// </summary>
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

            var targets = SelectPreferredTargets(source, resolved, preserveSourceSignalName: true)
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

        /// <summary>
        /// Resolves the runtime targets.
        /// </summary>
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

            var selectedEndpoints = SelectPreferredTargets(source, resolvedEndpoints, preserveSourceSignalName: forWrite)
                .OrderBy(endpoint => endpoint.Role == WireVizConnectorRole.Device ? 0 : endpoint.Role == WireVizConnectorRole.Harness ? 1 : 2)
                .ThenBy(endpoint => IsGenericBoundaryLabel(endpoint.PinLabel) ? 1 : 0)
                .ThenByDescending(endpoint => endpoint.Key.Count(ch => ch == '.'))
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

                var signalName = GetRuntimeSignalName(source, endpoint, preserveSourceSignalName: forWrite);
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

        /// <summary>
        /// Measures the active resistance path between two resolved endpoints.
        /// </summary>
        public WireVizResistanceMeasurement MeasureResistance(
            WireVizEndpoint source,
            WireVizEndpoint target,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults,
            string sourceSignalName,
            string targetSignalName)
        {
            if (string.Equals(source.Key, target.Key, StringComparison.OrdinalIgnoreCase))
            {
                return new WireVizResistanceMeasurement(
                    sourceSignalName,
                    targetSignalName,
                    true,
                    true,
                    true,
                    0d,
                    new[] { FormatNode(source.Key) },
                    Array.Empty<string>(),
                    SourcePath);
            }

            var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [source.Key] = 0d
            };
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var parentDescriptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var queue = new PriorityQueue<string, double>();
            queue.Enqueue(source.Key, 0d);

            while (queue.Count > 0)
            {
                queue.TryDequeue(out var current, out var currentDistance);
                if (current == null || currentDistance > distances[current] + 0.0000001d)
                {
                    continue;
                }

                if (string.Equals(current, target.Key, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                foreach (var edge in EnumerateAdjacentNodes(current, signalState, signalTimes, currentTimeMs, faults, forWrite: false))
                {
                    if (edge.IsSyntheticSignal)
                    {
                        continue;
                    }

                    var nextDistance = currentDistance + edge.ResistanceOhms;
                    if (distances.TryGetValue(edge.Target, out var knownDistance) && knownDistance <= nextDistance)
                    {
                        continue;
                    }

                    distances[edge.Target] = nextDistance;
                    parents[edge.Target] = current;
                    parentDescriptions[edge.Target] = edge.Description;
                    queue.Enqueue(edge.Target, nextDistance);
                }
            }

            if (!distances.TryGetValue(target.Key, out var totalResistance) ||
                !TryReconstructPath(source.Key, target.Key, parents, out var path))
            {
                return new WireVizResistanceMeasurement(
                    sourceSignalName,
                    targetSignalName,
                    true,
                    true,
                    false,
                    null,
                    sourceDocumentPath: SourcePath,
                    failureReason: $"Kein aktiver Pfad zwischen {source.DisplayName} und {target.DisplayName}.");
            }

            var nodes = path.Select(FormatNode).ToList();
            var edgeDescriptions = path
                .Skip(1)
                .Select(node => parentDescriptions.TryGetValue(node, out var description) ? description : null)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Select(description => description!)
                .ToList();

            return new WireVizResistanceMeasurement(
                sourceSignalName,
                targetSignalName,
                true,
                true,
                true,
                totalResistance,
                nodes,
                edgeDescriptions,
                SourcePath);
        }

        /// <summary>
        /// Executes Traverse.
        /// </summary>
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

        /// <summary>
        /// Executes EnumerateAdjacentNodes.
        /// </summary>
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

        /// <summary>
        /// Executes SelectPreferredTargets.
        /// </summary>
        private IReadOnlyList<WireVizEndpoint> SelectPreferredTargets(
            WireVizEndpoint source,
            IReadOnlyList<WireVizEndpoint> resolved,
            bool preserveSourceSignalName)
        {
            var candidates = resolved
                .Where(endpoint => !string.Equals(endpoint.Key, source.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var deviceTargets = candidates
                .Where(IsDeviceLikeEndpoint)
                .ToList();
            if (deviceTargets.Count > 0)
            {
                candidates = deviceTargets;
            }
            else
            {
                var preferredTargets = candidates
                    .Where(endpoint => endpoint.Role is WireVizConnectorRole.Harness or WireVizConnectorRole.Device)
                    .ToList();
                if (preferredTargets.Count > 0)
                {
                    candidates = preferredTargets;
                }
            }

            return candidates
                .GroupBy(endpoint => GetRuntimeSignalName(source, endpoint, preserveSourceSignalName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(GetTargetSpecificityScore)
                    .ThenBy(endpoint => endpoint.Key, StringComparer.OrdinalIgnoreCase)
                    .First())
                .ToList();
        }

        /// <summary>
        /// Executes GetTargetSpecificityScore.
        /// </summary>
        private static int GetTargetSpecificityScore(WireVizEndpoint endpoint)
        {
            var score = 0;
            score += IsDeviceLikeEndpoint(endpoint) ? 100 : endpoint.Role switch
            {
                WireVizConnectorRole.Harness => 50,
                _ => 0
            };

            if (!IsGenericBoundaryLabel(endpoint.PinLabel))
            {
                score += 20;
            }

            if (endpoint.Designator.Contains("DevicePort", StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }

            if (endpoint.Designator.Contains("BoardPort", StringComparison.OrdinalIgnoreCase))
            {
                score -= 30;
            }

            score += endpoint.Key.Count(ch => ch == '.') * 10;
            score += endpoint.Designator.Count(ch => ch == '.');
            return score;
        }

        /// <summary>
        /// Executes GetRuntimeSignalName.
        /// </summary>
        private static string GetRuntimeSignalName(WireVizEndpoint source, WireVizEndpoint target, bool preserveSourceSignalName)
        {
            if (preserveSourceSignalName &&
                !string.IsNullOrWhiteSpace(source.PinLabel) &&
                !IsGenericBoundaryLabel(source.PinLabel))
            {
                return source.PinLabel!;
            }

            if (IsDeviceLikeEndpoint(target) &&
                !string.IsNullOrWhiteSpace(target.PinLabel) &&
                !IsGenericBoundaryLabel(target.PinLabel))
            {
                return target.PinLabel!;
            }

            if (preserveSourceSignalName && !string.IsNullOrWhiteSpace(source.PinLabel))
            {
                return source.PinLabel!;
            }

            if (!string.IsNullOrWhiteSpace(source.PinLabel))
            {
                return source.PinLabel!;
            }

            return target.Key;
        }

        /// <summary>
        /// Executes IsGenericBoundaryLabel.
        /// </summary>
        private static bool IsGenericBoundaryLabel(string? label)
        {
            return !string.IsNullOrWhiteSpace(label) && GenericBoundaryLabels.Contains(label.Trim());
        }

        /// <summary>
        /// Executes IsDeviceLikeEndpoint.
        /// </summary>
        private static bool IsDeviceLikeEndpoint(WireVizEndpoint endpoint)
        {
            return endpoint.Role == WireVizConnectorRole.Device ||
                   endpoint.Designator.Contains("DevicePort", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Executes TryReconstructPath.
        /// </summary>
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

        /// <summary>
        /// Executes FormatNode.
        /// </summary>
        private string FormatNode(string key)
        {
            if (key.StartsWith("@signal:", StringComparison.OrdinalIgnoreCase))
            {
                return $"Signal {key["@signal:".Length..]}";
            }

            var endpoint = ResolveEndpointByKey(key).FirstOrDefault();
            return endpoint?.DisplayName ?? key;
        }

        /// <summary>
        /// Executes ResolveEndpointByKey.
        /// </summary>
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
            /// <summary>
            /// Initializes a new instance of the <see cref="TraversalResult"/> class.
            /// </summary>
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

            /// <summary>
            /// Gets the visited.
            /// </summary>
            public HashSet<string> Visited { get; }
            /// <summary>
            /// Gets the parents.
            /// </summary>
            public Dictionary<string, string> Parents { get; }
            /// <summary>
            /// Gets the source to node scale.
            /// </summary>
            public Dictionary<string, double> SourceToNodeScale { get; }
            /// <summary>
            /// Gets the synthetic targets.
            /// </summary>
            public IReadOnlyList<SyntheticRuntimeTarget> SyntheticTargets { get; }
        }

        private sealed class TraversalEdge
        {
            /// <summary>
            /// Initializes a new instance of TraversalEdge.
            /// </summary>
            private TraversalEdge(string target, double scale, double resistanceOhms, string? description, bool isSyntheticSignal, bool writeOnly, bool readOnly)
            {
                Target = target;
                Scale = scale;
                ResistanceOhms = resistanceOhms;
                Description = description;
                IsSyntheticSignal = isSyntheticSignal;
                WriteOnly = writeOnly;
                ReadOnly = readOnly;
            }

            /// <summary>
            /// Gets the target.
            /// </summary>
            public string Target { get; }
            /// <summary>
            /// Gets the scale.
            /// </summary>
            public double Scale { get; }
            /// <summary>
            /// Gets the resistance ohms.
            /// </summary>
            public double ResistanceOhms { get; }
            /// <summary>
            /// Gets the description.
            /// </summary>
            public string? Description { get; }
            /// <summary>
            /// Gets a value indicating whether the synthetic signal condition is met.
            /// </summary>
            public bool IsSyntheticSignal { get; }
            /// <summary>
            /// Gets the write only.
            /// </summary>
            public bool WriteOnly { get; }
            /// <summary>
            /// Gets the read only.
            /// </summary>
            public bool ReadOnly { get; }

            /// <summary>
            /// Executes node.
            /// </summary>
            public static TraversalEdge Node(string target, double scale = 1d, string? description = null, double resistanceOhms = 0d) =>
                new(target, scale, resistanceOhms, description, false, false, false);

            /// <summary>
            /// Executes synthetic signal.
            /// </summary>
            public static TraversalEdge SyntheticSignal(string signalName, double scale, string? description, bool writeOnly = false, bool readOnly = false) =>
                new($"@signal:{signalName}", scale, 0d, description, true, writeOnly, readOnly);
        }

        private sealed class SyntheticRuntimeTarget
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SyntheticRuntimeTarget"/> class.
            /// </summary>
            public SyntheticRuntimeTarget(string key, double sourceToTargetScale, string? description, bool writeOnly, bool readOnly)
            {
                Key = key;
                SourceToTargetScale = sourceToTargetScale;
                Description = description;
                WriteOnly = writeOnly;
                ReadOnly = readOnly;
            }

            /// <summary>
            /// Gets the key.
            /// </summary>
            public string Key { get; }
            /// <summary>
            /// Gets the signal name.
            /// </summary>
            public string SignalName => Key.StartsWith("@signal:", StringComparison.OrdinalIgnoreCase)
                ? Key["@signal:".Length..]
                : Key;
            /// <summary>
            /// Gets the source to target scale.
            /// </summary>
            public double SourceToTargetScale { get; }
            /// <summary>
            /// Gets the description.
            /// </summary>
            public string? Description { get; }
            /// <summary>
            /// Gets the write only.
            /// </summary>
            public bool WriteOnly { get; }
            /// <summary>
            /// Gets the read only.
            /// </summary>
            public bool ReadOnly { get; }
        }
    }

}
