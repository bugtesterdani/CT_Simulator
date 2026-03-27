// Provides Wire Viz Export Document for the Altium exporter WireViz runtime support.
namespace Ct3xxAltiumWireVizExporter.WireViz;

/// <summary>
/// Represents the wire viz export document.
/// </summary>
public sealed class WireVizExportDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizExportDocument"/> class.
    /// </summary>
    public WireVizExportDocument(
        string boardName,
        IReadOnlyList<WireVizConnectorExport> connectors,
        IReadOnlyList<WireVizConnectionExport> connections)
    {
        BoardName = boardName;
        Connectors = connectors;
        Connections = connections;
    }

    /// <summary>
    /// Gets the board name.
    /// </summary>
    public string BoardName { get; }
    /// <summary>
    /// Gets the connectors.
    /// </summary>
    public IReadOnlyList<WireVizConnectorExport> Connectors { get; }
    /// <summary>
    /// Gets the connections.
    /// </summary>
    public IReadOnlyList<WireVizConnectionExport> Connections { get; }
}

/// <summary>
/// Represents the wire viz connector export.
/// </summary>
public sealed class WireVizConnectorExport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizConnectorExport"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the type.
    /// </summary>
    public string Type { get; }
    /// <summary>
    /// Gets the subtype.
    /// </summary>
    public string Subtype { get; }
    /// <summary>
    /// Gets the role.
    /// </summary>
    public string Role { get; }
    /// <summary>
    /// Gets the pins.
    /// </summary>
    public IReadOnlyList<string> Pins { get; }
    /// <summary>
    /// Gets the pin labels.
    /// </summary>
    public IReadOnlyList<string> PinLabels { get; }
}

/// <summary>
/// Represents the wire viz connection export.
/// </summary>
public sealed class WireVizConnectionExport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizConnectionExport"/> class.
    /// </summary>
    public WireVizConnectionExport(string netName, IReadOnlyList<WireVizConnectionEndpointExport> endpoints)
    {
        NetName = netName;
        Endpoints = endpoints;
    }

    /// <summary>
    /// Gets the net name.
    /// </summary>
    public string NetName { get; }
    /// <summary>
    /// Gets the endpoints.
    /// </summary>
    public IReadOnlyList<WireVizConnectionEndpointExport> Endpoints { get; }
}

/// <summary>
/// Represents the wire viz connection endpoint export.
/// </summary>
public sealed class WireVizConnectionEndpointExport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizConnectionEndpointExport"/> class.
    /// </summary>
    public WireVizConnectionEndpointExport(string connectorName, IReadOnlyList<string> pins)
    {
        ConnectorName = connectorName;
        Pins = pins;
    }

    /// <summary>
    /// Gets the connector name.
    /// </summary>
    public string ConnectorName { get; }
    /// <summary>
    /// Gets the pins.
    /// </summary>
    public IReadOnlyList<string> Pins { get; }
}
