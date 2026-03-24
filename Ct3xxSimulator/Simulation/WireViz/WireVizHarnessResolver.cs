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
using Ct3xxSimulator.Simulation.FaultInjection;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed partial class WireVizHarnessResolver
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
        return TryResolve(signalOrTestpoint, signalState, null, 0L, SimulationFaultSet.Empty, out resolutions);
    }

    public bool TryResolve(
        string signalOrTestpoint,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults,
        out IReadOnlyList<WireVizConnectionResolution> resolutions)
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
                seed.Graph.GetConnectedEndpoints(seed.Source, signalState, signalTimes, currentTimeMs, faults)
                    .Where(target => !string.Equals(target.Key, seed.Source.Key, StringComparison.OrdinalIgnoreCase))
                    .DistinctBy(target => target.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                seed.Graph.SourcePath))
            .Where(item => item.Targets.Count > 0)
            .ToList();
        return resolutions.Count > 0;
    }

    public bool TryTrace(string signalOrTestpoint, IReadOnlyDictionary<string, object?> signalState, out IReadOnlyList<WireVizSignalTrace> traces)
    {
        return TryTrace(signalOrTestpoint, signalState, null, 0L, SimulationFaultSet.Empty, out traces);
    }

    public bool TryTrace(
        string signalOrTestpoint,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults,
        out IReadOnlyList<WireVizSignalTrace> traces)
    {
        if (string.IsNullOrWhiteSpace(signalOrTestpoint))
        {
            traces = Array.Empty<WireVizSignalTrace>();
            return false;
        }

        if (!_resolutionSeeds.TryGetValue(signalOrTestpoint.Trim(), out var seeds))
        {
            traces = Array.Empty<WireVizSignalTrace>();
            return false;
        }

        traces = seeds
            .SelectMany(seed => seed.Graph.TraceSignalPaths(seed.Source, signalState, signalTimes, currentTimeMs, faults))
            .DistinctBy(trace => $"{trace.SignalName}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .ToList();
        return traces.Count > 0;
    }

    public bool TryResolveRuntimeTargets(
        string signalOrTestpoint,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults,
        bool forWrite,
        out IReadOnlyList<WireVizRuntimeTarget> targets)
    {
        if (string.IsNullOrWhiteSpace(signalOrTestpoint))
        {
            targets = Array.Empty<WireVizRuntimeTarget>();
            return false;
        }

        if (!_resolutionSeeds.TryGetValue(signalOrTestpoint.Trim(), out var seeds))
        {
            targets = Array.Empty<WireVizRuntimeTarget>();
            return false;
        }

        targets = seeds
            .SelectMany(seed => seed.Graph.ResolveRuntimeTargets(seed.Source, signalState, signalTimes, currentTimeMs, faults, forWrite))
            .GroupBy(target => target.SignalName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        return targets.Count > 0;
    }

    public IReadOnlyList<string> DescribeRelayStates(IReadOnlyDictionary<string, object?> signalState)
    {
        return DescribeRelayStates(signalState, null, 0L, SimulationFaultSet.Empty);
    }

    public IReadOnlyList<string> DescribeRelayStates(
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults)
    {
        return _simulationElements
            .OfType<RelayElementDefinition>()
            .OrderBy(relay => relay.Id, StringComparer.OrdinalIgnoreCase)
            .Select(relay => $"{relay.Id}: {(WireVizGraph.IsRelayClosed(relay, signalState, signalTimes, currentTimeMs, faults) ? "geschlossen" : "offen")} via {relay.Coil.Signal}")
            .ToList();
    }

    public IReadOnlyList<string> DescribeElementStates(
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults)
    {
        var states = new List<string>();
        foreach (var element in _simulationElements.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
        {
            switch (element)
            {
                case RelayElementDefinition relay:
                    states.Add($"{relay.Id}: relay {(WireVizGraph.IsRelayClosed(relay, signalState, signalTimes, currentTimeMs, faults) ? "geschlossen" : "offen")}");
                    break;
                case ResistorElementDefinition resistor:
                    states.Add($"{resistor.Id}: resistor {resistor.Ohms.ToString("0.###", CultureInfo.InvariantCulture)} Ohm");
                    break;
                case TransformerElementDefinition transformer:
                    states.Add($"{transformer.Id}: transformer ratio {transformer.Ratio.ToString("0.###", CultureInfo.InvariantCulture)}");
                    break;
                case CurrentTransformerElementDefinition currentTransformer:
                    states.Add($"{currentTransformer.Id}: current_transformer ratio {currentTransformer.Ratio.ToString("0.###", CultureInfo.InvariantCulture)} signal {currentTransformer.PrimarySignal}");
                    break;
                case UnknownElementDefinition unknown:
                    states.Add(DescribeUnknownElementState(unknown, signalState, signalTimes, currentTimeMs, faults));
                    break;
            }
        }

        return states;
    }

    private static string DescribeUnknownElementState(
        UnknownElementDefinition definition,
        IReadOnlyDictionary<string, object?> signalState,
        IReadOnlyDictionary<string, long>? signalTimes,
        long currentTimeMs,
        SimulationFaultSet faults)
    {
        var lowerType = definition.Type.ToLowerInvariant();
        return lowerType switch
        {
            "switch" => $"{definition.Id}: switch {(WireVizGraph.IsGenericSwitchClosed(definition, signalState, signalTimes, currentTimeMs) ? "geschlossen" : "offen")}",
            "fuse" => $"{definition.Id}: fuse {(faults.IsBlownFuse(definition.Id) ? "ausgeloest" : "intakt")}",
            "diode" => $"{definition.Id}: diode {ReadMetadataValue(definition, "anode")} -> {ReadMetadataValue(definition, "cathode")}",
            "load" => $"{definition.Id}: load {ReadMetadataValue(definition, "ohms")} Ohm",
            "voltage_divider" => $"{definition.Id}: voltage_divider ratio {ReadMetadataValue(definition, "ratio")}",
            "sensor" => $"{definition.Id}: sensor {ReadMetadataValue(definition, "input_signal")} -> {ReadMetadataValue(definition, "output_signal")}",
            "opto" => $"{definition.Id}: opto {(WireVizGraph.IsGenericSwitchClosed(definition, signalState, signalTimes, currentTimeMs) ? "aktiv" : "inaktiv")}",
            "transistor" => $"{definition.Id}: transistor {(WireVizGraph.IsGenericSwitchClosed(definition, signalState, signalTimes, currentTimeMs) ? "leitend" : "sperrt")}",
            _ => $"{definition.Id}: {definition.Type}"
        };
    }

    private static string ReadMetadataValue(UnknownElementDefinition definition, string key)
    {
        return definition.Metadata.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
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
