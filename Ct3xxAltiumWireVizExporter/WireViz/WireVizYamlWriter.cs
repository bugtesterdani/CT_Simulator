// Provides Wire Viz Yaml Writer for the Altium exporter WireViz runtime support.
using System.Text;

namespace Ct3xxAltiumWireVizExporter.WireViz;

public static class WireVizYamlWriter
{
    /// <summary>
    /// Executes write.
    /// </summary>
    public static string Write(WireVizExportDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Generated from Altium connectivity export");
        builder.AppendLine($"# Board: {document.BoardName}");
        builder.AppendLine("connectors:");

        foreach (var connector in document.Connectors)
        {
            builder.AppendLine($"  {connector.Name}:");
            builder.AppendLine($"    type: {Quote(connector.Type)}");
            builder.AppendLine($"    subtype: {Quote(connector.Subtype)}");
            builder.AppendLine($"    role: {Quote(connector.Role)}");
            builder.AppendLine($"    pins: [{string.Join(", ", connector.Pins.Select(Quote))}]");
            builder.AppendLine($"    pinlabels: [{string.Join(", ", connector.PinLabels.Select(Quote))}]");
        }

        builder.AppendLine("connections:");
        foreach (var connection in document.Connections)
        {
            builder.AppendLine($"  # Net: {connection.NetName}");
            builder.AppendLine("  -");
            foreach (var endpoint in connection.Endpoints)
            {
                builder.AppendLine($"    - {endpoint.ConnectorName}: [{string.Join(", ", endpoint.Pins.Select(Quote))}]");
            }
        }

        return builder.ToString();
    }

    private static string Quote(string text)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
