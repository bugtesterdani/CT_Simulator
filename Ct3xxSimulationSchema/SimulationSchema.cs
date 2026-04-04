using Ct3xxSimulationModelParser;
using YamlDotNet.Serialization;

namespace Ct3xxSimulationSchema;

/// <summary>
/// Builds a merged schema from existing simulation.yaml files.
/// </summary>
public sealed class SimulationSchemaBuilder
{
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSchemaBuilder"/> class.
    /// </summary>
    public SimulationSchemaBuilder()
    {
        _deserializer = new DeserializerBuilder().Build();
    }

    /// <summary>
    /// Builds a schema document by scanning a root directory.
    /// </summary>
    public SimulationSchema Build(string rootDirectory)
    {
        var schema = new SimulationSchema();
        var files = Directory.GetFiles(rootDirectory, "simulation.yaml", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var data = _deserializer.Deserialize<object>(yaml);
                schema.Merge(data);
            }
            catch
            {
                // ignore malformed examples
            }
        }
        schema.ApplyDefaults();
        return schema;
    }
}

/// <summary>
/// Represents a merged simulation schema (keys only).
/// </summary>
public sealed class SimulationSchema
{
    /// <summary>
    /// Gets the top-level keys.
    /// </summary>
    public HashSet<string> TopLevelKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the nested keys for each top-level section.
    /// </summary>
    public Dictionary<string, HashSet<string>> SectionKeys { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets known element type values from examples.
    /// </summary>
    public HashSet<string> ElementTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets known element keys per element type.
    /// </summary>
    public Dictionary<string, HashSet<string>> ElementTypeKeys { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets known value hints for properties.
    /// </summary>
    public Dictionary<string, HashSet<string>> PropertyValueHints { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets required keys per element type.
    /// </summary>
    public Dictionary<string, HashSet<string>> RequiredElementTypeKeys { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets help text for element types (multi-language).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> ElementTypeHelp { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets element types that allow free-form metadata keys.
    /// </summary>
    public HashSet<string> FreeFormElementTypes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets optional metadata keys for generic element types.
    /// </summary>
    public Dictionary<string, HashSet<string>> GenericElementOptionalKeys { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets structured field templates by element type.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> ElementFieldTemplates { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Gets UI schemas for structured editors.
    /// </summary>
    public Dictionary<string, Dictionary<string, UiFieldSchema>> ElementFieldEditors { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Merges a yaml object into the schema.
    /// </summary>
    public void Merge(object? data)
    {
        if (data is IDictionary<object, object> map)
        {
            foreach (var pair in map)
            {
                if (pair.Key is not string key)
                {
                    continue;
                }
                TopLevelKeys.Add(key);
                if (pair.Value is IDictionary<object, object> section)
                {
                    var keySet = GetOrCreate(key);
                    foreach (var inner in section)
                    {
                        if (inner.Key is string innerKey)
                        {
                            keySet.Add(innerKey);
                        }
                    }
                }
            }
            CaptureElementTypes(map);
        }
    }

    /// <summary>
    /// Applies parser-supported defaults when no examples are present.
    /// </summary>
    public void ApplyDefaults()
    {
        foreach (var key in SimulationModelSchema.TopLevelKeys)
        {
            TopLevelKeys.Add(key);
        }

        foreach (var type in SimulationModelSchema.ElementTypes)
        {
            ElementTypes.Add(type);
        }

        foreach (var pair in SimulationModelSchema.ElementTypeKeys)
        {
            AddElementTypeKeys(pair.Key, pair.Value.ToArray());
        }

        foreach (var pair in SimulationModelSchema.RequiredElementTypeKeys)
        {
            AddRequiredElementTypeKeys(pair.Key, pair.Value.ToArray());
        }

        var elementKeys = GetOrCreate("elements");
        foreach (var key in new[] { "id", "type" })
        {
            elementKeys.Add(key);
        }

        foreach (var pair in SimulationModelSchema.PropertyValueHints)
        {
            AddPropertyValueHints(pair.Key, pair.Value.ToArray());
        }

        foreach (var pair in SimulationModelSchema.ElementTypeHelp)
        {
            ElementTypeHelp[pair.Key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = pair.Value.TryGetValue("en", out var en) ? en : string.Empty,
                ["de"] = pair.Value.TryGetValue("de", out var de) ? de : string.Empty
            };
        }

        foreach (var type in SimulationModelSchema.FreeFormElementTypes)
        {
            FreeFormElementTypes.Add(type);
        }

        foreach (var pair in SimulationModelSchema.GenericElementOptionalKeys)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in pair.Value)
            {
                set.Add(key);
            }
            GenericElementOptionalKeys[pair.Key] = set;
        }

        foreach (var pair in SimulationModelSchema.ElementFieldTemplates)
        {
            var cloned = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pair.Value)
            {
                cloned[entry.Key] = entry.Value;
            }
            ElementFieldTemplates[pair.Key] = cloned;
        }

        foreach (var pair in SimulationFieldUiSchema.ElementFieldEditors)
        {
            var cloned = new Dictionary<string, UiFieldSchema>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pair.Value)
            {
                cloned[entry.Key] = entry.Value;
            }
            ElementFieldEditors[pair.Key] = cloned;
        }
    }

    private HashSet<string> GetOrCreate(string key)
    {
        if (!SectionKeys.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SectionKeys[key] = set;
        }
        return set;
    }

    private void AddElementTypeKeys(string type, params string[] keys)
    {
        if (!ElementTypeKeys.TryGetValue(type, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ElementTypeKeys[type] = set;
        }

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    private void AddRequiredElementTypeKeys(string type, params string[] keys)
    {
        if (!RequiredElementTypeKeys.TryGetValue(type, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RequiredElementTypeKeys[type] = set;
        }

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    private void AddPropertyValueHints(string key, params string[] values)
    {
        if (!PropertyValueHints.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PropertyValueHints[key] = set;
        }

        foreach (var value in values)
        {
            set.Add(value);
        }
    }

    private void CaptureElementTypes(IDictionary<object, object> map)
    {
        if (!TryGet(map, "elements", out var elements) || elements is not IEnumerable<object> sequence)
        {
            return;
        }

        foreach (var item in sequence)
        {
            if (item is not IDictionary<object, object> element)
            {
                continue;
            }

            if (TryGet(element, "type", out var typeValue))
            {
                var text = typeValue?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ElementTypes.Add(text!);
                }
            }
        }
    }

    private static bool TryGet(IDictionary<object, object> map, string key, out object? value)
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
}
