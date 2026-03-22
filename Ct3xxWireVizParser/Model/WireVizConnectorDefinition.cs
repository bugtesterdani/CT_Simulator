using System;

namespace Ct3xxWireVizParser.Model;

public sealed class WireVizConnectorDefinition
{
    public WireVizConnectorDefinition(string designator, WireVizValue value)
    {
        if (string.IsNullOrWhiteSpace(designator))
        {
            throw new ArgumentException("Designator must be provided.", nameof(designator));
        }

        Designator = designator.Trim();
        Value = value ?? throw new ArgumentNullException(nameof(value));
        BackgroundColor = ResolveBackgroundColor(value);
        IsUppercaseDesignator = IsUppercase(Designator);
        Role = ResolveRole(BackgroundColor, IsUppercaseDesignator);
    }

    public string Designator { get; }
    public WireVizValue Value { get; }
    public string? BackgroundColor { get; }
    public bool IsUppercaseDesignator { get; }
    public WireVizConnectorRole Role { get; }

    public bool IsEmphasized => IsYellow(BackgroundColor);

    private static string? ResolveBackgroundColor(WireVizValue value)
    {
        if (value.TryGetProperty("bgcolor", out var background))
        {
            return background.AsString();
        }

        if (value.TryGetProperty("color", out var color))
        {
            return color.AsString();
        }

        return null;
    }

    private static WireVizConnectorRole ResolveRole(string? backgroundColor, bool isUppercaseDesignator)
    {
        var isYellow = IsYellow(backgroundColor);
        if (isYellow && isUppercaseDesignator)
        {
            return WireVizConnectorRole.Harness;
        }

        if (isYellow)
        {
            return WireVizConnectorRole.Device;
        }

        if (isUppercaseDesignator)
        {
            return WireVizConnectorRole.TestSystem;
        }

        return WireVizConnectorRole.Unknown;
    }

    private static bool IsUppercase(string value)
    {
        var hasLetter = false;
        foreach (var ch in value)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            hasLetter = true;
            if (!char.IsUpper(ch))
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static bool IsYellow(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        var normalized = color.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        return normalized is "YE" or "YEL" or "YELLOW" or "#FFFF00" or "#FF0";
    }
}
