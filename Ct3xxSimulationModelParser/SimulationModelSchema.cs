using System.Collections.Generic;

namespace Ct3xxSimulationModelParser;

/// <summary>
/// Single source of truth for parser-supported simulation model schema.
/// </summary>
public static class SimulationModelSchema
{
    /// <summary>
    /// Gets supported top-level sections.
    /// </summary>
    public static IReadOnlyCollection<string> TopLevelKeys { get; } = new[]
    {
        "elements"
    };

    /// <summary>
    /// Gets supported element types.
    /// </summary>
    public static IReadOnlyCollection<string> ElementTypes { get; } = new[]
    {
        "relay",
        "resistor",
        "inductor",
        "transformer",
        "current_transformer",
        "limit",
        "assembly",
        "tester_supply",
        "tester_output",
        "testsystem",
        "switch",
        "fuse",
        "diode",
        "load",
        "voltage_divider",
        "sensor",
        "opto",
        "transistor"
    };

    /// <summary>
    /// Gets element types that allow free-form metadata keys.
    /// </summary>
    public static IReadOnlyCollection<string> FreeFormElementTypes { get; } = new[]
    {
        "switch",
        "fuse",
        "diode",
        "load",
        "voltage_divider",
        "sensor",
        "opto",
        "transistor"
    };

    /// <summary>
    /// Gets optional metadata keys for generic element types.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> GenericElementOptionalKeys { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["switch"] = new[] { "model", "state", "default_state", "delay_ms", "note" },
            ["fuse"] = new[] { "model", "rating_a", "trip_a", "delay_ms", "note" },
            ["diode"] = new[] { "model", "direction", "vf", "threshold_v", "note" },
            ["load"] = new[] { "model", "ohms", "watts", "current_a", "voltage_v", "note" },
            ["voltage_divider"] = new[] { "model", "ratio", "r1", "r2", "note" },
            ["sensor"] = new[] { "model", "unit", "range_min", "range_max", "note" },
            ["opto"] = new[] { "model", "current_a", "voltage_v", "note" },
            ["transistor"] = new[] { "model", "transistor_type", "gain", "threshold_v", "note" }
        };

    /// <summary>
    /// Gets JSON templates for structured fields by element type.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> ElementFieldTemplates { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["relay"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["coil"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["signal"] = "UIF_OUT1",
                    ["threshold_v"] = 24.0
                },
                ["contacts"] = new List<Dictionary<string, object>>
                {
                    new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["a"] = "RELAIS.COM1",
                        ["b"] = "RELAIS.NO1",
                        ["mode"] = "normally_open"
                    }
                }
            },
            ["assembly"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["ports"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["IN"] = "BoardPort.IN",
                    ["OUT"] = "BoardPort.OUT"
                }
            }
        };

    /// <summary>
    /// Gets known keys per element type (required + optional).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> ElementTypeKeys { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["relay"] = new[] { "id", "type", "coil", "contacts", "delay_ms" },
            ["resistor"] = new[] { "id", "type", "a", "b", "ohms" },
            ["inductor"] = new[] { "id", "type", "a", "b", "henry", "inductance" },
            ["transformer"] = new[] { "id", "type", "primary_a", "primary_b", "secondary_a", "secondary_b", "ratio" },
            ["current_transformer"] = new[] { "id", "type", "primary_signal", "secondary_a", "secondary_b", "ratio" },
            ["limit"] = new[] { "id", "type", "mode", "nodes", "node_prefixes", "max_voltage", "max_current", "gain" },
            ["assembly"] = new[] { "id", "type", "wiring", "simulation", "ports" },
            ["tester_supply"] = new[] { "id", "type", "signal", "voltage" },
            ["tester_output"] = new[] { "id", "type", "signal", "high_mode", "low_mode", "high_supply", "low_supply", "high_value", "low_value" },
            ["testsystem"] = new[] { "id", "type", "odbc_mode", "odbc_mock_result", "odbc_timeout_seconds", "odbc_timeout_ms" },
            ["switch"] = new[] { "id", "type" },
            ["fuse"] = new[] { "id", "type" },
            ["diode"] = new[] { "id", "type" },
            ["load"] = new[] { "id", "type" },
            ["voltage_divider"] = new[] { "id", "type" },
            ["sensor"] = new[] { "id", "type" },
            ["opto"] = new[] { "id", "type" },
            ["transistor"] = new[] { "id", "type", "transistor_type" }
        };

    /// <summary>
    /// Gets required keys per element type.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> RequiredElementTypeKeys { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["relay"] = new[] { "id", "type", "coil.signal", "coil.threshold_v", "contacts" },
            ["resistor"] = new[] { "id", "type", "a", "b", "ohms" },
            ["inductor"] = new[] { "id", "type", "a", "b", "henry|inductance" },
            ["transformer"] = new[] { "id", "type", "primary_a", "primary_b", "secondary_a", "secondary_b", "ratio" },
            ["current_transformer"] = new[] { "id", "type", "primary_signal", "secondary_a", "secondary_b", "ratio" },
            ["limit"] = new[] { "id", "type", "nodes|node_prefixes" },
            ["assembly"] = new[] { "id", "type", "wiring", "simulation", "ports" },
            ["tester_supply"] = new[] { "id", "type", "signal", "voltage" },
            ["tester_output"] = new[] { "id", "type", "signal", "high_mode", "low_mode" },
            ["testsystem"] = new[] { "id", "type" },
            ["switch"] = new[] { "id", "type" },
            ["fuse"] = new[] { "id", "type" },
            ["diode"] = new[] { "id", "type" },
            ["load"] = new[] { "id", "type" },
            ["voltage_divider"] = new[] { "id", "type" },
            ["sensor"] = new[] { "id", "type" },
            ["opto"] = new[] { "id", "type" },
            ["transistor"] = new[] { "id", "type" }
        };

    /// <summary>
    /// Gets known value hints for specific properties.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> PropertyValueHints { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["high_mode"] = new[] { "supply", "value", "open" },
            ["low_mode"] = new[] { "supply", "value", "open" },
            ["mode"] = new[] { "voltage", "current", "normally_open", "normally_closed", "no", "nc" },
            ["odbc_mode"] = new[] { "real", "mock" }
        };

    /// <summary>
    /// Gets help text for element types (based on OPTIONS.md).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ElementTypeHelp { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["relay"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Relay with coil + contacts. Required: coil.signal, coil.threshold_v, contacts. Optional: delay_ms.",
                ["de"] = "Relais mit Spule und Kontakten. Pflicht: coil.signal, coil.threshold_v, contacts. Optional: delay_ms."
            },
            ["resistor"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Resistor between a and b. Required: a, b, ohms.",
                ["de"] = "Widerstand zwischen a und b. Pflicht: a, b, ohms."
            },
            ["inductor"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Inductor between a and b. Required: a, b, henry or inductance.",
                ["de"] = "Induktivitaet zwischen a und b. Pflicht: a, b, henry oder inductance."
            },
            ["transformer"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Transformer. Required: primary_a/b, secondary_a/b, ratio (primary/secondary).",
                ["de"] = "Transformator. Pflicht: primary_a/b, secondary_a/b, ratio (primary/secondary)."
            },
            ["current_transformer"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Current transformer. Required: primary_signal, secondary_a/b, ratio.",
                ["de"] = "Stromwandler. Pflicht: primary_signal, secondary_a/b, ratio."
            },
            ["assembly"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Sub-assembly with its own wiring + simulation. Required: wiring, simulation, ports.",
                ["de"] = "Unterbaugruppe mit eigener Verdrahtung/Simulation. Pflicht: wiring, simulation, ports."
            },
            ["tester_supply"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Declared tester supply. Required: signal, voltage.",
                ["de"] = "Deklarierte Pruefsystem-Spannungsquelle. Pflicht: signal, voltage."
            },
            ["tester_output"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Tester output (IOXX). Required: signal, high_mode, low_mode. Modes: supply/value/open.",
                ["de"] = "Pruefsystem-Ausgang (IOXX). Pflicht: signal, high_mode, low_mode. Modi: supply/value/open."
            },
            ["testsystem"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Global test system options (ODBC). Fields: odbc_mode, odbc_mock_result, timeout.",
                ["de"] = "Globale Testsystem-Optionen (ODBC). Felder: odbc_mode, odbc_mock_result, timeout."
            },
            ["switch"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Generic switch element. Parameters via metadata.",
                ["de"] = "Generischer Schalter. Parameter ueber Metadaten."
            },
            ["fuse"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Fuse element. Parameters via metadata.",
                ["de"] = "Sicherung. Parameter ueber Metadaten."
            },
            ["diode"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Diode element. Parameters via metadata.",
                ["de"] = "Diode. Parameter ueber Metadaten."
            },
            ["load"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Load element. Parameters via metadata.",
                ["de"] = "Last. Parameter ueber Metadaten."
            },
            ["voltage_divider"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Voltage divider element. Parameters via metadata.",
                ["de"] = "Spannungsteiler. Parameter ueber Metadaten."
            },
            ["sensor"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Sensor element. Parameters via metadata.",
                ["de"] = "Sensor. Parameter ueber Metadaten."
            },
            ["opto"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Optocoupler element. Parameters via metadata.",
                ["de"] = "Optokoppler. Parameter ueber Metadaten."
            },
            ["transistor"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Transistor element. Prefer transistor_type metadata.",
                ["de"] = "Transistor. Bevorzugt transistor_type Metadaten."
            }
        };
}
