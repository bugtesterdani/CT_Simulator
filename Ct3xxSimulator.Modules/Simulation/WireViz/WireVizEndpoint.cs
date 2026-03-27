using System;
using Ct3xxWireVizParser.Model;

namespace Ct3xxSimulator.Simulation.WireViz;

/// <summary>
/// Represents one concrete connector endpoint in the flattened WireViz graph.
/// </summary>
public sealed class WireVizEndpoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizEndpoint"/> class.
    /// </summary>
    /// <param name="designator">The connector or element designator.</param>
    /// <param name="pin">The pin identifier within the designator.</param>
    /// <param name="pinLabel">The optional logical pin label.</param>
    /// <param name="role">The connector role in the graph.</param>
    /// <param name="backgroundColor">The optional connector background color.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="designator"/> or <paramref name="pin"/> is missing.</exception>
    public WireVizEndpoint(string designator, string pin, string? pinLabel, WireVizConnectorRole role, string? backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(designator))
        {
            throw new ArgumentException("Designator must be provided.", nameof(designator));
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("Pin must be provided.", nameof(pin));
        }

        Designator = designator.Trim();
        Pin = pin.Trim();
        PinLabel = string.IsNullOrWhiteSpace(pinLabel) ? null : pinLabel.Trim();
        Role = role;
        BackgroundColor = string.IsNullOrWhiteSpace(backgroundColor) ? null : backgroundColor.Trim();
    }

    /// <summary>
    /// Gets the connector or element designator.
    /// </summary>
    public string Designator { get; }
    /// <summary>
    /// Gets the pin identifier within the designator.
    /// </summary>
    public string Pin { get; }
    /// <summary>
    /// Gets the optional logical pin label.
    /// </summary>
    public string? PinLabel { get; }
    /// <summary>
    /// Gets the connector role in the graph.
    /// </summary>
    public WireVizConnectorRole Role { get; }
    /// <summary>
    /// Gets the optional connector background color.
    /// </summary>
    public string? BackgroundColor { get; }

    /// <summary>
    /// Gets the unique graph key of the endpoint.
    /// </summary>
    public string Key => $"{Designator}.{Pin}";
    /// <summary>
    /// Gets a display string that combines the key and optional pin label.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(PinLabel)
        ? Key
        : $"{Key} ({PinLabel})";
}
