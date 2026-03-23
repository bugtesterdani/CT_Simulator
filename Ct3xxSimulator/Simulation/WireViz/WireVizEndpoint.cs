using System;
using Ct3xxWireVizParser.Model;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizEndpoint
{
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

    public string Designator { get; }
    public string Pin { get; }
    public string? PinLabel { get; }
    public WireVizConnectorRole Role { get; }
    public string? BackgroundColor { get; }

    public string Key => $"{Designator}.{Pin}";
    public string DisplayName => string.IsNullOrWhiteSpace(PinLabel)
        ? Key
        : $"{Key} ({PinLabel})";
}
