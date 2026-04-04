using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser;

/// <summary>
/// UI schema describing structured JSON editors per element field.
/// </summary>
public static class SimulationFieldUiSchema
{
    /// <summary>
    /// Gets UI schemas keyed by element type and field name.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, UiFieldSchema>> ElementFieldEditors { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, UiFieldSchema>>(StringComparer.OrdinalIgnoreCase)
        {
            ["relay"] = new Dictionary<string, UiFieldSchema>(StringComparer.OrdinalIgnoreCase)
            {
                ["coil"] = UiFieldSchema.Object(new[]
                {
                    UiFieldSchema.Text("signal", "Signal", required: true),
                    UiFieldSchema.Number("threshold_v", "Threshold (V)", required: true)
                }),
                ["contacts"] = UiFieldSchema.Array(
                    UiFieldSchema.Object(new[]
                    {
                        UiFieldSchema.Text("a", "Contact A", required: true),
                        UiFieldSchema.Text("b", "Contact B", required: true),
                        UiFieldSchema.Select("mode", "Mode", new[]
                        {
                            "normally_open",
                            "normally_closed"
                        }, required: true)
                    }),
                    "Contacts")
            },
            ["assembly"] = new Dictionary<string, UiFieldSchema>(StringComparer.OrdinalIgnoreCase)
            {
                ["ports"] = UiFieldSchema.Map("Ports", "Port name", "Node path")
            }
        };
}

/// <summary>
/// UI schema node for structured editors.
/// </summary>
public sealed class UiFieldSchema
{
    public UiFieldSchema(
        string type,
        string label,
        bool required,
        IReadOnlyList<UiFieldSchema> fields,
        IReadOnlyList<string> options,
        UiFieldSchema? item,
        string keyLabel,
        string valueLabel)
    {
        Type = type;
        Label = label;
        Required = required;
        Fields = fields;
        Options = options;
        Item = item;
        KeyLabel = keyLabel;
        ValueLabel = valueLabel;
    }

    public string Type { get; }
    public string Label { get; }
    public bool Required { get; }
    public IReadOnlyList<UiFieldSchema> Fields { get; }
    public IReadOnlyList<string> Options { get; }
    public UiFieldSchema? Item { get; }
    public string KeyLabel { get; }
    public string ValueLabel { get; }

    public static UiFieldSchema Text(string name, string label, bool required = false)
        => new("text", name, required, System.Array.Empty<UiFieldSchema>(), System.Array.Empty<string>(), null, label, string.Empty);

    public static UiFieldSchema Number(string name, string label, bool required = false)
        => new("number", name, required, System.Array.Empty<UiFieldSchema>(), System.Array.Empty<string>(), null, label, string.Empty);

    public static UiFieldSchema Select(string name, string label, IReadOnlyList<string> options, bool required = false)
        => new("select", name, required, System.Array.Empty<UiFieldSchema>(), options, null, label, string.Empty);

    public static UiFieldSchema Object(IReadOnlyList<UiFieldSchema> fields)
        => new("object", string.Empty, false, fields, System.Array.Empty<string>(), null, string.Empty, string.Empty);

    public static UiFieldSchema Array(UiFieldSchema item, string label)
        => new("array", string.Empty, false, System.Array.Empty<UiFieldSchema>(), System.Array.Empty<string>(), item, label, string.Empty);

    public static UiFieldSchema Map(string label, string keyLabel, string valueLabel)
        => new("map", string.Empty, false, System.Array.Empty<UiFieldSchema>(), System.Array.Empty<string>(), null, keyLabel, valueLabel);
}
