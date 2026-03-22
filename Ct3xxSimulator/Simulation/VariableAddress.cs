using System;
using System.Globalization;

namespace Ct3xxSimulator.Simulation;

public readonly struct VariableAddress
{
    public VariableAddress(string name, int? index)
    {
        Name = name;
        Index = index;
    }

    public string Name { get; }
    public int? Index { get; }

    public bool HasIndex => Index.HasValue;

    public static VariableAddress From(string name)
    {
        if (!TryParse(name, out var address))
        {
            throw new FormatException($"Cannot parse variable reference '{name}'.");
        }

        return address;
    }

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

    public override string ToString() => HasIndex ? $"{Name}[{Index}]" : Name;
}
