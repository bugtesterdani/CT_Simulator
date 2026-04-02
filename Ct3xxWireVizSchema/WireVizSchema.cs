namespace Ct3xxWireVizSchema;

/// <summary>
/// Central registry for WireViz known keys and hints.
/// </summary>
public static class WireVizSchema
{
    /// <summary>
    /// Reference commit hash for syntax definition.
    /// </summary>
    public const string ReferenceCommit = "e4fe099f8c7b86736aee7b4227cc794b6e8b36f0";

    /// <summary>
    /// Known connector keys per WireViz syntax.
    /// </summary>
    public static readonly IReadOnlyList<string> ConnectorKeys = new[]
    {
        "type", "subtype", "color", "image", "notes",
        "ignore_in_bom", "pn", "manufacturer", "mpn", "supplier", "spn",
        "additional_components",
        "pincount", "pins", "pinlabels", "pincolors",
        "bgcolor", "bgcolor_title", "style", "show_name", "show_pincount",
        "hide_disconnected_pins", "loops",
    };

    /// <summary>
    /// Known cable keys per WireViz syntax.
    /// </summary>
    public static readonly IReadOnlyList<string> CableKeys = new[]
    {
        "category", "type", "gauge", "show_equiv", "length", "shield", "color", "image", "notes",
        "ignore_in_bom", "pn", "manufacturer", "mpn", "supplier", "spn", "additional_components",
        "wirecount", "colors", "color_code", "wirelabels",
        "bgcolor", "bgcolor_title", "show_name", "show_wirecount", "show_wirenumbers",
    };

    /// <summary>
    /// Known global section keys.
    /// </summary>
    public static readonly IReadOnlyList<string> GlobalSections = new[]
    {
        "metadata", "options", "tweak", "additional_bom_items"
    };

    /// <summary>
    /// Known property type hints for UI helpers.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PropertyTypeHints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ignore_in_bom"] = "bool",
            ["show_name"] = "bool",
            ["show_pincount"] = "bool",
            ["hide_disconnected_pins"] = "bool",
            ["show_equiv"] = "bool",
            ["show_wirecount"] = "bool",
            ["show_wirenumbers"] = "bool",
            ["pincount"] = "number",
            ["wirecount"] = "number",
            ["pins"] = "array",
            ["pinlabels"] = "array",
            ["pincolors"] = "array",
            ["loops"] = "array",
            ["colors"] = "array",
            ["wirelabels"] = "array",
            ["additional_components"] = "array"
        };
}
