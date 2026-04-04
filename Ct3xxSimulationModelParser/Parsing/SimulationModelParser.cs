// Provides Simulation Model Parser for the simulation model parser parsing support.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ct3xxSimulationModelParser.Model;
using YamlDotNet.Serialization;

namespace Ct3xxSimulationModelParser.Parsing;

/// <summary>
/// Represents the simulation model parser.
/// </summary>
public sealed class SimulationModelParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// Parses the file.
    /// </summary>
    public SimulationModelDocument ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Simulation model file '{filePath}' was not found.", filePath);
        }

        using var reader = File.OpenText(filePath);
        var raw = _deserializer.Deserialize<Dictionary<object, object?>>(reader);
        return ParseRoot(raw, filePath);
    }

    /// <summary>
    /// Parses the yaml content.
    /// </summary>
    public SimulationModelDocument ParseText(string yaml, string sourcePath = "<inline>")
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("Yaml must be provided.", nameof(yaml));
        }

        var raw = _deserializer.Deserialize<Dictionary<object, object?>>(yaml);
        return ParseRoot(raw, sourcePath);
    }

    /// <summary>
    /// Executes ParseRoot.
    /// </summary>
    private static SimulationModelDocument ParseRoot(IDictionary<object, object?>? root, string sourcePath)
    {
        if (root == null)
        {
            throw new InvalidDataException($"[{sourcePath}] Simulation root is empty.");
        }

        if (!TryGet(root, "elements", out var elementsNode) || elementsNode is not IEnumerable<object> items)
        {
            throw new InvalidDataException($"[{sourcePath}] Section 'elements' is missing.");
        }

        var elements = new List<SimulationElementDefinition>();
        foreach (var item in items)
        {
            if (item is not IDictionary<object, object?> map)
            {
                continue;
            }

            elements.Add(ParseElement(map, sourcePath));
        }

        return new SimulationModelDocument(sourcePath, elements);
    }

    /// <summary>
    /// Executes ParseElement.
    /// </summary>
    private static SimulationElementDefinition ParseElement(IDictionary<object, object?> map, string sourcePath)
    {
        var id = RequireString(map, "id", sourcePath);
        var type = RequireString(map, "type", sourcePath).ToLowerInvariant();
        var metadata = ToFlatStringMap(map);

        switch (type)
        {
            case "relay":
                return new RelayElementDefinition(
                    id,
                    ParseRelayCoil(RequireMap(map, "coil", sourcePath), sourcePath),
                    ParseRelayContacts(RequireSequence(map, "contacts", sourcePath), sourcePath),
                    metadata);
            case "resistor":
                return new ResistorElementDefinition(
                    id,
                    RequireString(map, "a", sourcePath),
                    RequireString(map, "b", sourcePath),
                    RequireDouble(map, "ohms", sourcePath),
                    metadata);
            case "inductor":
                return new InductorElementDefinition(
                    id,
                    RequireString(map, "a", sourcePath),
                    RequireString(map, "b", sourcePath),
                    RequireDoubleAny(map, sourcePath, "henry", "inductance"),
                    metadata);
            case "transformer":
                return new TransformerElementDefinition(
                    id,
                    RequireString(map, "primary_a", sourcePath),
                    RequireString(map, "primary_b", sourcePath),
                    RequireString(map, "secondary_a", sourcePath),
                    RequireString(map, "secondary_b", sourcePath),
                    RequireDouble(map, "ratio", sourcePath),
                    metadata);
            case "current_transformer":
                return new CurrentTransformerElementDefinition(
                    id,
                    RequireString(map, "primary_signal", sourcePath),
                    RequireString(map, "secondary_a", sourcePath),
                    RequireString(map, "secondary_b", sourcePath),
                    RequireDouble(map, "ratio", sourcePath),
                    metadata);
            case "limit":
                return new LimitElementDefinition(
                    id,
                    OptionalString(map, "mode") ?? "voltage",
                    ParseNodePrefixes(map, sourcePath),
                    OptionalDouble(map, "max_voltage", sourcePath),
                    OptionalDouble(map, "max_current", sourcePath),
                    OptionalDouble(map, "gain", sourcePath) ?? 1d,
                    metadata);
            case "assembly":
                return new AssemblyElementDefinition(
                    id,
                    RequireString(map, "wiring", sourcePath),
                    OptionalString(map, "simulation"),
                    ParsePorts(RequireMap(map, "ports", sourcePath)),
                    metadata);
            default:
                return new UnknownElementDefinition(id, type, metadata);
        }
    }

    /// <summary>
    /// Executes ParseRelayCoil.
    /// </summary>
    private static RelayCoilDefinition ParseRelayCoil(IDictionary<object, object?> map, string sourcePath)
    {
        return new RelayCoilDefinition(
            RequireString(map, "signal", sourcePath),
            RequireDouble(map, "threshold_v", sourcePath));
    }

    /// <summary>
    /// Executes ParseRelayContacts.
    /// </summary>
    private static IReadOnlyList<RelayContactDefinition> ParseRelayContacts(IEnumerable<object> nodes, string sourcePath)
    {
        var contacts = new List<RelayContactDefinition>();
        foreach (var node in nodes)
        {
            if (node is not IDictionary<object, object?> map)
            {
                continue;
            }

            contacts.Add(new RelayContactDefinition(
                RequireString(map, "a", sourcePath),
                RequireString(map, "b", sourcePath),
                RequireString(map, "mode", sourcePath)));
        }

        return contacts;
    }

    /// <summary>
    /// Executes ToFlatStringMap.
    /// </summary>
    private static IReadOnlyDictionary<string, string?> ToFlatStringMap(IDictionary<object, object?> source)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            if (entry.Key == null)
            {
                continue;
            }

            result[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString();
        }

        return result;
    }

    /// <summary>
    /// Executes ParsePorts.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParsePorts(IDictionary<object, object?> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            var key = entry.Key?.ToString()?.Trim();
            var value = entry.Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[key!] = value!;
        }

        return result;
    }

    /// <summary>
    /// Executes ParseNodePrefixes.
    /// </summary>
    private static IReadOnlyList<string> ParseNodePrefixes(IDictionary<object, object?> map, string sourcePath)
    {
        if (!TryGet(map, "nodes", out var nodes) && !TryGet(map, "node_prefixes", out nodes))
        {
            throw new InvalidDataException($"[{sourcePath}] Required sequence 'nodes' is missing.");
        }

        var result = new List<string>();
        if (nodes is IEnumerable<object> sequence)
        {
            foreach (var item in sequence)
            {
                var text = item?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(text!);
                }
            }
        }
        else
        {
            var single = nodes?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(single))
            {
                result.Add(single!);
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException($"[{sourcePath}] Required sequence 'nodes' is empty.");
        }

        return result;
    }

    /// <summary>
    /// Executes TryGet.
    /// </summary>
    private static bool TryGet(IDictionary<object, object?> map, string key, out object? value)
    {
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Executes RequireString.
    /// </summary>
    private static string RequireString(IDictionary<object, object?> map, string key, string sourcePath)
    {
        if (!TryGet(map, key, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
        {
            throw new InvalidDataException($"[{sourcePath}] Required string '{key}' is missing.");
        }

        return value!.ToString()!.Trim();
    }

    /// <summary>
    /// Executes OptionalString.
    /// </summary>
    private static string? OptionalString(IDictionary<object, object?> map, string key)
    {
        return TryGet(map, key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
            ? value?.ToString()?.Trim()
            : null;
    }

    /// <summary>
    /// Executes RequireDouble.
    /// </summary>
    private static double RequireDouble(IDictionary<object, object?> map, string key, string sourcePath)
    {
        var text = RequireString(map, key, sourcePath).Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidDataException($"[{sourcePath}] '{key}' is not a valid number.");
        }

        return value;
    }

    /// <summary>
    /// Executes RequireDoubleAny.
    /// </summary>
    private static double RequireDoubleAny(IDictionary<object, object?> map, string sourcePath, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGet(map, key, out var value) && !string.IsNullOrWhiteSpace(value?.ToString()))
            {
                var text = value!.ToString()!.Trim().Replace(',', '.');
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new InvalidDataException($"[{sourcePath}] '{key}' is not a valid number.");
                }

                return parsed;
            }
        }

        throw new InvalidDataException($"[{sourcePath}] Required value missing ({string.Join(" or ", keys)}).");
    }

    /// <summary>
    /// Executes OptionalDouble.
    /// </summary>
    private static double? OptionalDouble(IDictionary<object, object?> map, string key, string sourcePath)
    {
        if (!TryGet(map, key, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
        {
            return null;
        }

        var text = value!.ToString()!.Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"[{sourcePath}] '{key}' is not a valid number.");
        }

        return parsed;
    }

    /// <summary>
    /// Executes RequireMap.
    /// </summary>
    private static IDictionary<object, object?> RequireMap(IDictionary<object, object?> map, string key, string sourcePath)
    {
        if (!TryGet(map, key, out var value) || value is not IDictionary<object, object?> nested)
        {
            throw new InvalidDataException($"[{sourcePath}] Required map '{key}' is missing.");
        }

        return nested;
    }

    /// <summary>
    /// Executes RequireSequence.
    /// </summary>
    private static IEnumerable<object> RequireSequence(IDictionary<object, object?> map, string key, string sourcePath)
    {
        if (!TryGet(map, key, out var value) || value is not IEnumerable<object> sequence)
        {
            throw new InvalidDataException($"[{sourcePath}] Required sequence '{key}' is missing.");
        }

        return sequence;
    }
}
