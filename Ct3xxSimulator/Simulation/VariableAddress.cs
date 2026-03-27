using System;
using System.Globalization;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Represents a parsed CT3xx variable reference, optionally including an array index.
/// </summary>
public readonly struct VariableAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VariableAddress"/> struct.
    /// </summary>
    /// <param name="name">The scalar or array name.</param>
    /// <param name="index">The optional one-based array index.</param>
    public VariableAddress(string name, int? index)
    {
        Name = name;
        Index = index;
    }

    /// <summary>
    /// Gets the scalar or array name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the optional one-based array index.
    /// </summary>
    public int? Index { get; }

    /// <summary>
    /// Gets a value indicating whether the address refers to an indexed array entry.
    /// </summary>
    public bool HasIndex => Index.HasValue;

    /// <summary>
    /// Parses a textual variable reference and throws when the text is invalid.
    /// </summary>
    public static VariableAddress From(string name)
    {
        if (!TryParse(name, out var address))
        {
            throw new FormatException($"Cannot parse variable reference '{name}'.");
        }

        return address;
    }

    /// <summary>
    /// Attempts to parse a textual CT3xx variable reference.
    /// </summary>
    /// <param name="raw">The raw variable reference text.</param>
    /// <param name="address">When successful, receives the parsed address.</param>
    /// <returns><see langword="true"/> when parsing succeeded.</returns>
    public static bool TryParse(string? raw, out VariableAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        raw = raw.Trim();
        var start = raw.IndexOf('[');
        if (start >= 0)
        {
            var end = raw.IndexOf(']', start + 1);
            if (end < 0)
            {
                return false;
            }

            var name = raw[..start].Trim();
            var indexText = raw[(start + 1)..end];
            if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                return false;
            }

            address = new VariableAddress(name, index);
            return true;
        }

        address = new VariableAddress(raw, null);
        return true;
    }

    /// <summary>
    /// Executes to string.
    /// </summary>
    public override string ToString() => HasIndex ? $"{Name}[{Index}]" : Name;
}
