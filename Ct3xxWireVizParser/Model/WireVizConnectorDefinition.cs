// Provides Wire Viz Connector Definition for the WireViz parser model support.
using System;

namespace Ct3xxWireVizParser.Model;

/// <summary>
/// Represents the wire viz connector definition.
/// </summary>
public sealed class WireVizConnectorDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizConnectorDefinition"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the designator.
    /// </summary>
    public string Designator { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public WireVizValue Value { get; }
    /// <summary>
    /// Gets the background color.
    /// </summary>
    public string? BackgroundColor { get; }
    /// <summary>
    /// Gets a value indicating whether the uppercase designator condition is met.
    /// </summary>
    public bool IsUppercaseDesignator { get; }
    /// <summary>
    /// Gets the role.
    /// </summary>
    public WireVizConnectorRole Role { get; }

    /// <summary>
    /// Determines whether the yellow condition is met.
    /// </summary>
    public bool IsEmphasized => IsYellow(BackgroundColor);

    /// <summary>
    /// Executes ResolveBackgroundColor.
    /// </summary>
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

    /// <summary>
    /// Executes ResolveRole.
    /// </summary>
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

    /// <summary>
    /// Executes IsUppercase.
    /// </summary>
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

    /// <summary>
    /// Executes IsYellow.
    /// </summary>
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
