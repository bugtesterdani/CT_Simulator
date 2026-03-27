// Provides Altium Connectivity Record for the Altium exporter Altium import support.
namespace Ct3xxAltiumWireVizExporter.Altium;

/// <summary>
/// Represents the altium connectivity record.
/// </summary>
public sealed class AltiumConnectivityRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AltiumConnectivityRecord"/> class.
    /// </summary>
    public AltiumConnectivityRecord(
        string netName,
        string designator,
        string pin,
        string connectorName,
        string role,
        string? pinName)
    {
        NetName = netName;
        Designator = designator;
        Pin = pin;
        ConnectorName = connectorName;
        Role = role;
        PinName = pinName;
    }

    /// <summary>
    /// Gets the net name.
    /// </summary>
    public string NetName { get; }
    /// <summary>
    /// Gets the designator.
    /// </summary>
    public string Designator { get; }
    /// <summary>
    /// Gets the pin.
    /// </summary>
    public string Pin { get; }
    /// <summary>
    /// Gets the connector name.
    /// </summary>
    public string ConnectorName { get; }
    /// <summary>
    /// Gets the role.
    /// </summary>
    public string Role { get; }
    /// <summary>
    /// Gets the pin name.
    /// </summary>
    public string? PinName { get; }

    /// <summary>
    /// Gets the pin label.
    /// </summary>
    public string PinLabel => $"{Designator}_{Pin}";
}
