using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulationModelParser.Parsing;
using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxSimulator.Validation;

internal static class SimulationModelDeepValidator
{
    public static IReadOnlyList<string> Validate(string? wiringFolderPath, string? simulationModelFolderPath)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(wiringFolderPath) || !Directory.Exists(wiringFolderPath))
        {
            return issues;
        }

        var wireFiles = Directory.EnumerateFiles(wiringFolderPath, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(wiringFolderPath, "*.yaml", SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parser = new WireVizParser();
        foreach (var file in wireFiles)
        {
            try
            {
                var document = parser.ParseFile(file);
                ValidateWireVizDocument(document, issues);
            }
            catch (Exception ex)
            {
                issues.Add($"WireViz-Datei '{Path.GetFileName(file)}' konnte nicht geparst werden: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(simulationModelFolderPath) && Directory.Exists(simulationModelFolderPath))
        {
            var simulationPath = Directory.EnumerateFiles(simulationModelFolderPath, "simulation.y*ml", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(simulationPath))
            {
                ValidateSimulationRecursive(simulationPath!, issues, new Stack<string>(), null);
            }
        }

        return issues;
    }

    private static void ValidateWireVizDocument(WireVizDocument document, List<string> issues)
    {
        var connectorPins = BuildConnectorPins(document);
        var usage = connectorPins.Keys.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var connector in document.Connectors.Keys)
        {
            ValidateName(connector, "Steckername", issues, document.SourcePath ?? string.Empty);
        }

        foreach (var connection in document.Connections)
        {
            var segments = connection.AsSequenceOrEmpty()
                .Select(ParseConnectionSegment)
                .Where(item => item != null)
                .Cast<ConnectionSegment>()
                .ToList();

            foreach (var segment in segments)
            {
                if (!connectorPins.ContainsKey(segment.Designator))
                {
                    issues.Add($"WireViz '{Path.GetFileName(document.SourcePath)}': unbekannter Stecker '{segment.Designator}' in Verbindung.");
                    continue;
                }

                foreach (var terminal in segment.Terminals)
                {
                    var key = $"{segment.Designator}.{terminal}";
                    if (!connectorPins.ContainsKey(key))
                    {
                        issues.Add($"WireViz '{Path.GetFileName(document.SourcePath)}': unbekannter Pin '{key}'.");
                        continue;
                    }

                    usage[key]++;
                    ValidateName(terminal, "Pinname", issues, document.SourcePath ?? string.Empty);
                }
            }

            var width = segments.Max(item => item.Terminals.Count);
            for (var terminalIndex = 0; terminalIndex < width; terminalIndex++)
            {
                var path = new List<string>();
                foreach (var segment in segments)
                {
                    var terminal = segment.GetTerminal(terminalIndex);
                    if (!string.IsNullOrWhiteSpace(terminal))
                    {
                        path.Add($"{segment.Designator}.{terminal}");
                    }
                }

                for (var pathIndex = 0; pathIndex < path.Count - 1; pathIndex++)
                {
                    AddGraphEdge(adjacency, path[pathIndex], path[pathIndex + 1]);
                    AddGraphEdge(adjacency, path[pathIndex + 1], path[pathIndex]);
                }
            }
        }

        foreach (var unused in usage.Where(item => item.Value == 0))
        {
            issues.Add($"WireViz '{Path.GetFileName(document.SourcePath)}': unverbundener Pin '{unused.Key}'.");
        }

        foreach (var cycle in FindCycles(adjacency).Take(10))
        {
            issues.Add($"WireViz '{Path.GetFileName(document.SourcePath)}': Zyklus erkannt ({cycle}).");
        }
    }

    private static void ValidateSimulationRecursive(string simulationPath, List<string> issues, Stack<string> stack, WireVizDocument? parentWireViz)
    {
        var fullPath = Path.GetFullPath(simulationPath);
        if (stack.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add($"Simulation-Zyklus erkannt: {string.Join(" -> ", stack.Reverse().Append(fullPath).Select(Path.GetFileName))}");
            return;
        }

        stack.Push(fullPath);
        var parser = new SimulationModelParser();
        SimulationModelDocument document;
        try
        {
            document = parser.ParseFile(fullPath);
        }
        catch (Exception ex)
        {
            issues.Add($"simulation.yaml '{Path.GetFileName(fullPath)}' konnte nicht geparst werden: {ex.Message}");
            stack.Pop();
            return;
        }

        var baseDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var localWirePath = Path.Combine(baseDirectory, "Verdrahtung.yml");
        if (!File.Exists(localWirePath))
        {
            localWirePath = Path.Combine(baseDirectory, "Verdrahtung.yaml");
        }

        WireVizDocument? localWireViz = parentWireViz;
        if (File.Exists(localWirePath))
        {
            try
            {
                localWireViz = new WireVizParser().ParseFile(localWirePath);
            }
            catch (Exception ex)
            {
                issues.Add($"WireViz fuer '{Path.GetFileName(fullPath)}' konnte nicht geparst werden: {ex.Message}");
            }
        }

        var knownNodes = localWireViz == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : BuildConnectorPins(localWireViz).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var elementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var elementNodeMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Elements)
        {
            ValidateName(element.Id, "Element-ID", issues, fullPath);
            if (!elementIds.Add(element.Id))
            {
                issues.Add($"Simulation '{Path.GetFileName(fullPath)}': doppelte Element-ID '{element.Id}'.");
            }

            var referencedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            elementNodeMap[element.Id] = referencedNodes;

            switch (element)
            {
                case RelayElementDefinition relay:
                    ValidateNode(relay.Coil.Signal, "Relais-Spulensignal", issues, fullPath, knownNodes, allowSignalOnly: true);
                    foreach (var contact in relay.Contacts)
                    {
                        ValidateNode(contact.A, $"Relaiskontakt {relay.Id}.A", issues, fullPath, knownNodes);
                        ValidateNode(contact.B, $"Relaiskontakt {relay.Id}.B", issues, fullPath, knownNodes);
                        AddIfNode(referencedNodes, contact.A);
                        AddIfNode(referencedNodes, contact.B);
                    }
                    break;
                case ResistorElementDefinition resistor:
                    ValidateNode(resistor.A, $"Resistor {resistor.Id}.A", issues, fullPath, knownNodes);
                    ValidateNode(resistor.B, $"Resistor {resistor.Id}.B", issues, fullPath, knownNodes);
                    AddIfNode(referencedNodes, resistor.A);
                    AddIfNode(referencedNodes, resistor.B);
                    break;
                case TransformerElementDefinition transformer:
                    ValidateNode(transformer.PrimaryA, $"Transformer {transformer.Id}.PrimaryA", issues, fullPath, knownNodes);
                    ValidateNode(transformer.PrimaryB, $"Transformer {transformer.Id}.PrimaryB", issues, fullPath, knownNodes);
                    ValidateNode(transformer.SecondaryA, $"Transformer {transformer.Id}.SecondaryA", issues, fullPath, knownNodes);
                    ValidateNode(transformer.SecondaryB, $"Transformer {transformer.Id}.SecondaryB", issues, fullPath, knownNodes);
                    AddIfNode(referencedNodes, transformer.PrimaryA);
                    AddIfNode(referencedNodes, transformer.PrimaryB);
                    AddIfNode(referencedNodes, transformer.SecondaryA);
                    AddIfNode(referencedNodes, transformer.SecondaryB);
                    break;
                case CurrentTransformerElementDefinition currentTransformer:
                    ValidateNode(currentTransformer.SecondaryA, $"CurrentTransformer {currentTransformer.Id}.SecondaryA", issues, fullPath, knownNodes);
                    ValidateNode(currentTransformer.SecondaryB, $"CurrentTransformer {currentTransformer.Id}.SecondaryB", issues, fullPath, knownNodes);
                    AddIfNode(referencedNodes, currentTransformer.SecondaryA);
                    AddIfNode(referencedNodes, currentTransformer.SecondaryB);
                    if (string.IsNullOrWhiteSpace(currentTransformer.PrimarySignal))
                    {
                        issues.Add($"Simulation '{Path.GetFileName(fullPath)}': current_transformer '{currentTransformer.Id}' ohne primary_signal.");
                    }
                    break;
                case AssemblyElementDefinition assembly:
                    if (localWireViz == null || !localWireViz.Connectors.ContainsKey(assembly.Id))
                    {
                        issues.Add($"Simulation '{Path.GetFileName(fullPath)}': assembly '{assembly.Id}' fehlt als Stecker im WireViz.");
                    }

                    var wiringPath = ResolvePath(baseDirectory, assembly.Wiring);
                    if (!File.Exists(wiringPath))
                    {
                        issues.Add($"Simulation '{Path.GetFileName(fullPath)}': assembly '{assembly.Id}' verweist auf fehlende wiring-Datei '{assembly.Wiring}'.");
                    }

                    var simulationChildPath = string.IsNullOrWhiteSpace(assembly.Simulation) ? null : ResolvePath(baseDirectory, assembly.Simulation!);
                    if (!string.IsNullOrWhiteSpace(simulationChildPath) && !File.Exists(simulationChildPath))
                    {
                        issues.Add($"Simulation '{Path.GetFileName(fullPath)}': assembly '{assembly.Id}' verweist auf fehlende simulation-Datei '{assembly.Simulation}'.");
                    }

                    if (localWireViz != null && localWireViz.Connectors.TryGetValue(assembly.Id, out var connectorValue))
                    {
                        var pins = ExpandPins(connectorValue);
                        foreach (var port in assembly.Ports)
                        {
                            if (!pins.Contains(port.Key, StringComparer.OrdinalIgnoreCase))
                            {
                                issues.Add($"Simulation '{Path.GetFileName(fullPath)}': assembly-Port '{assembly.Id}.{port.Key}' existiert nicht im WireViz.");
                            }

                            AddIfNode(referencedNodes, $"{assembly.Id}.{port.Key}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(simulationChildPath))
                    {
                        WireVizDocument? childWire = null;
                        if (File.Exists(wiringPath))
                        {
                            try
                            {
                                childWire = new WireVizParser().ParseFile(wiringPath);
                            }
                            catch
                            {
                            }
                        }

                        foreach (var port in assembly.Ports)
                        {
                            if (childWire != null && !BuildConnectorPins(childWire).ContainsKey(port.Value))
                            {
                                issues.Add($"Simulation '{Path.GetFileName(fullPath)}': assembly-Port-Mapping '{assembly.Id}.{port.Key} -> {port.Value}' zeigt auf unbekannten internen Pin.");
                            }

                            AddIfNode(referencedNodes, port.Value);
                        }

                        ValidateSimulationRecursive(simulationChildPath!, issues, stack, childWire);
                    }
                    break;
                case UnknownElementDefinition unknown:
                    ValidateUnknownElement(unknown, issues, fullPath, knownNodes);
                    foreach (var key in new[] { "a", "b", "anode", "cathode", "collector", "emitter", "drain", "source", "input", "output", "output_a", "output_b" })
                    {
                        if (unknown.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                        {
                            AddIfNode(referencedNodes, value!);
                        }
                    }
                    break;
            }
        }

        foreach (var issue in ValidateSimulationConnectivity(fullPath, elementNodeMap))
        {
            issues.Add(issue);
        }

        stack.Pop();
    }

    private static void ValidateUnknownElement(UnknownElementDefinition definition, List<string> issues, string sourcePath, HashSet<string> knownNodes)
    {
        foreach (var key in new[] { "a", "b", "anode", "cathode", "collector", "emitter", "drain", "source", "input", "output", "output_a", "output_b" })
        {
            if (definition.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                ValidateNode(value!, $"{definition.Type} {definition.Id}.{key}", issues, sourcePath, knownNodes);
            }
        }
    }

    private static void ValidateNode(string node, string label, List<string> issues, string sourcePath, HashSet<string> knownNodes, bool allowSignalOnly = false)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            issues.Add($"Simulation '{Path.GetFileName(sourcePath)}': {label} ist leer.");
            return;
        }

        ValidateName(node, label, issues, sourcePath, allowHierarchy: true);

        if (allowSignalOnly && !node.Contains(".", StringComparison.Ordinal))
        {
            return;
        }

        if (!knownNodes.Contains(node))
        {
            issues.Add($"Simulation '{Path.GetFileName(sourcePath)}': {label} verweist auf unbekannten Knoten '{node}'.");
        }
    }

    private static Dictionary<string, WireVizValue> BuildConnectorPins(WireVizDocument document)
    {
        var result = new Dictionary<string, WireVizValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var connector in document.Connectors)
        {
            foreach (var pin in ExpandPins(connector.Value))
            {
                result[$"{connector.Key}.{pin}"] = connector.Value;
            }
        }

        return result;
    }

    private static List<string> ExpandPins(WireVizValue connector)
    {
        if (connector.TryGetProperty("pins", out var pins))
        {
            var explicitPins = ExpandValue(pins);
            if (explicitPins.Count > 0)
            {
                return explicitPins;
            }
        }

        if (connector.TryGetProperty("pincount", out var pinCountValue) &&
            int.TryParse(pinCountValue.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) &&
            count > 0)
        {
            return Enumerable.Range(1, count).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToList();
        }

        return new List<string>();
    }

    private static List<string> ExpandValue(WireVizValue value)
    {
        if (value.Kind == WireVizValueKind.Sequence)
        {
            return value.Items.SelectMany(ExpandValue).ToList();
        }

        var text = value.AsString();
        return string.IsNullOrWhiteSpace(text) ? new List<string>() : ExpandToken(text.Trim());
    }

    private static ConnectionSegment? ParseConnectionSegment(WireVizValue value)
    {
        var properties = value.AsMappingOrEmpty();
        if (properties.Count != 1)
        {
            return null;
        }

        var pair = properties.First();
        return new ConnectionSegment(pair.Key, ExpandValue(pair.Value));
    }

    private static string ResolvePath(string baseDirectory, string relativeOrAbsolute)
    {
        return Path.GetFullPath(Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(baseDirectory, relativeOrAbsolute));
    }

    private static void ValidateName(string value, string label, List<string> issues, string sourcePath, bool allowHierarchy = false)
    {
        var candidate = value.Trim();
        var allowedExtra = allowHierarchy ? "._-" : "_-";
        foreach (var ch in candidate)
        {
            if (char.IsLetterOrDigit(ch) || allowedExtra.Contains(ch))
            {
                continue;
            }

            issues.Add($"'{Path.GetFileName(sourcePath)}': {label} '{value}' enthaelt ungueltiges Zeichen '{ch}'.");
            return;
        }
    }

    private static void AddIfNode(HashSet<string> nodes, string candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && candidate.Contains(".", StringComparison.Ordinal))
        {
            nodes.Add(candidate);
        }
    }

    private static IReadOnlyList<string> ValidateSimulationConnectivity(string sourcePath, IReadOnlyDictionary<string, HashSet<string>> elementNodeMap)
    {
        var issues = new List<string>();
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var elements = elementNodeMap.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < elements.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < elements.Count; otherIndex++)
            {
                var left = elements[index];
                var right = elements[otherIndex];
                if (!elementNodeMap[left].Overlaps(elementNodeMap[right]))
                {
                    continue;
                }

                AddGraphEdge(graph, left, right);
                AddGraphEdge(graph, right, left);
            }
        }

        foreach (var item in elementNodeMap.Where(item => item.Value.Count == 0))
        {
            issues.Add($"Simulation '{Path.GetFileName(sourcePath)}': Element '{item.Key}' ist isoliert und referenziert keine Verdrahtungsknoten.");
        }

        foreach (var item in elementNodeMap.Where(item => item.Value.Count > 0 && !graph.ContainsKey(item.Key)))
        {
            issues.Add($"Simulation '{Path.GetFileName(sourcePath)}': Element '{item.Key}' ist nicht mit anderen Simulationselementen verbunden.");
        }

        foreach (var cycle in FindCycles(graph).Take(10))
        {
            issues.Add($"Simulation '{Path.GetFileName(sourcePath)}': Element-Zyklus erkannt ({cycle}).");
        }

        return issues;
    }

    private static List<string> ExpandToken(string token)
    {
        var dashIndex = token.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= token.Length - 1)
        {
            return new List<string> { token };
        }

        var startText = token[..dashIndex];
        var endText = token[(dashIndex + 1)..];
        if (!int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !int.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            return new List<string> { token };
        }

        var step = start <= end ? 1 : -1;
        var result = new List<string>();
        for (var current = start; current != end + step; current += step)
        {
            result.Add(current.ToString(CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static void AddGraphEdge(Dictionary<string, HashSet<string>> graph, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        if (!graph.TryGetValue(from, out var targets))
        {
            targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            graph[from] = targets;
        }

        targets.Add(to);
    }

    private static IEnumerable<string> FindCycles(Dictionary<string, HashSet<string>> graph)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var cycle in FindCycles(node, null, graph, visited, stack, emitted))
            {
                yield return cycle;
            }
        }
    }

    private static IEnumerable<string> FindCycles(
        string node,
        string? parent,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        Stack<string> stack,
        HashSet<string> emitted)
    {
        if (stack.Any(item => string.Equals(item, node, StringComparison.OrdinalIgnoreCase)))
        {
            var cycleNodes = stack.Reverse().TakeWhile(item => !string.Equals(item, node, StringComparison.OrdinalIgnoreCase)).Reverse().Append(node).ToList();
            var key = string.Join("->", cycleNodes);
            if (emitted.Add(key))
            {
                yield return string.Join(" -> ", cycleNodes);
            }

            yield break;
        }

        if (!visited.Add(node))
        {
            yield break;
        }

        stack.Push(node);
        if (graph.TryGetValue(node, out var targets))
        {
            foreach (var target in targets)
            {
                if (string.Equals(target, parent, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var cycle in FindCycles(target, node, graph, visited, stack, emitted))
                {
                    yield return cycle;
                }
            }
        }

        stack.Pop();
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
}
