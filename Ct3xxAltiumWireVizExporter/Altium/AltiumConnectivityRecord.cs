namespace Ct3xxAltiumWireVizExporter.Altium;

public sealed class AltiumConnectivityRecord
{
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

    public string NetName { get; }
    public string Designator { get; }
    public string Pin { get; }
    public string ConnectorName { get; }
    public string Role { get; }
    public string? PinName { get; }

    public string PinLabel => $"{Designator}_{Pin}";
}
