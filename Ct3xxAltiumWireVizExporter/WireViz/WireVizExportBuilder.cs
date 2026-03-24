using Ct3xxAltiumWireVizExporter.Altium;
using Ct3xxAltiumWireVizExporter.Configuration;

namespace Ct3xxAltiumWireVizExporter.WireViz;

public static class WireVizExportBuilder
{
    public static WireVizExportDocument Build(IReadOnlyList<AltiumConnectivityRecord> records, ExportConfiguration configuration)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        var connectors = records
            .GroupBy(record => record.ConnectorName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedPins = group
                    .OrderBy(record => SortPin(record.Pin))
                    .ThenBy(record => record.Pin, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new WireVizConnectorExport(
                    group.Key,
                    configuration.ConnectorType,
                    configuration.ConnectorSubtype,
                    orderedPins.First().Role,
                    orderedPins.Select(record => record.Pin).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    orderedPins.Select(record => record.PinLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            })
            .ToList();

        var connections = records
            .GroupBy(record => record.NetName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new WireVizConnectionExport(
                group.Key,
                group.GroupBy(record => record.ConnectorName, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(connectorGroup => connectorGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(connectorGroup => new WireVizConnectionEndpointExport(
                        connectorGroup.Key,
                        connectorGroup.Select(record => record.Pin)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(SortPin)
                            .ThenBy(pin => pin, StringComparer.OrdinalIgnoreCase)
                            .ToList()))
                    .ToList()))
            .Where(connection => connection.Endpoints.Count >= 2)
            .ToList();

        return new WireVizExportDocument(configuration.BoardName, connectors, connections);
    }

    private static int SortPin(string pin)
    {
        return int.TryParse(pin, out var numeric) ? numeric : int.MaxValue;
    }
}
