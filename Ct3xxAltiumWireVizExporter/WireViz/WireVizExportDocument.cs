namespace Ct3xxAltiumWireVizExporter.WireViz;

public sealed class WireVizExportDocument
{
    public WireVizExportDocument(
        string boardName,
        IReadOnlyList<WireVizConnectorExport> connectors,
        IReadOnlyList<WireVizConnectionExport> connections)
    {
        BoardName = boardName;
        Connectors = connectors;
        Connections = connections;
    }

    public string BoardName { get; }
    public IReadOnlyList<WireVizConnectorExport> Connectors { get; }
    public IReadOnlyList<WireVizConnectionExport> Connections { get; }
}

public sealed class WireVizConnectorExport
{
    public WireVizConnectorExport(
        string name,
        string type,
        string subtype,
        string role,
        IReadOnlyList<string> pins,
        IReadOnlyList<string> pinLabels)
    {
        Name = name;
        Type = type;
        Subtype = subtype;
        Role = role;
        Pins = pins;
        PinLabels = pinLabels;
    }

    public string Name { get; }
    public string Type { get; }
    public string Subtype { get; }
    public string Role { get; }
    public IReadOnlyList<string> Pins { get; }
    public IReadOnlyList<string> PinLabels { get; }
}

public sealed class WireVizConnectionExport
{
    public WireVizConnectionExport(string netName, IReadOnlyList<WireVizConnectionEndpointExport> endpoints)
    {
        NetName = netName;
        Endpoints = endpoints;
    }

    public string NetName { get; }
    public IReadOnlyList<WireVizConnectionEndpointExport> Endpoints { get; }
}

public sealed class WireVizConnectionEndpointExport
{
    public WireVizConnectionEndpointExport(string connectorName, IReadOnlyList<string> pins)
    {
        ConnectorName = connectorName;
        Pins = pins;
    }

    public string ConnectorName { get; }
    public IReadOnlyList<string> Pins { get; }
}
