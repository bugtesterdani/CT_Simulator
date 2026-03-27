// Provides Export Configuration for the Altium exporter configuration support.
using System.Text.Json;

namespace Ct3xxAltiumWireVizExporter.Configuration;

/// <summary>
/// Represents the export configuration.
/// </summary>
public sealed class ExportConfiguration
{
    /// <summary>
    /// Gets the board name.
    /// </summary>
    public string BoardName { get; init; } = "AltiumBoard";
    /// <summary>
    /// Gets the connector type.
    /// </summary>
    public string ConnectorType { get; init; } = "altium_connector";
    /// <summary>
    /// Gets the connector subtype.
    /// </summary>
    public string ConnectorSubtype { get; init; } = "board_connector";
    /// <summary>
    /// Gets the default role.
    /// </summary>
    public string? DefaultRole { get; init; } = "harness";
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<string> ConnectorPrefixes { get; init; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public Dictionary<string, string> ConnectorAliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Executes new.
    /// </summary>
    public Dictionary<string, string> RoleMappings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<string> IncludeNets { get; init; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public List<string> ExcludeNets { get; init; } = new();

    /// <summary>
    /// Executes load.
    /// </summary>
    public static ExportConfiguration Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Configuration path must be provided.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Configuration file not found.", path);
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var configuration = JsonSerializer.Deserialize<ExportConfiguration>(json, options);
        if (configuration == null)
        {
            throw new InvalidOperationException("Could not deserialize export configuration.");
        }

        return configuration;
    }

    /// <summary>
    /// Determines whether the connector condition is met.
    /// </summary>
    public bool IsConnector(string designator, string? componentKind)
    {
        if (!string.IsNullOrWhiteSpace(componentKind) &&
            componentKind.Contains("connector", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ConnectorPrefixes.Count == 0)
        {
            return true;
        }

        return ConnectorPrefixes.Any(prefix => designator.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether the include net condition is met.
    /// </summary>
    public bool ShouldIncludeNet(string netName)
    {
        if (string.IsNullOrWhiteSpace(netName))
        {
            return false;
        }

        if (ExcludeNets.Any(net => string.Equals(net, netName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (IncludeNets.Count == 0)
        {
            return true;
        }

        return IncludeNets.Any(net => string.Equals(net, netName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the connector name.
    /// </summary>
    public string ResolveConnectorName(string designator)
    {
        return ConnectorAliases.TryGetValue(designator, out var alias) && !string.IsNullOrWhiteSpace(alias)
            ? alias.Trim()
            : designator.Trim();
    }

    /// <summary>
    /// Resolves the role.
    /// </summary>
    public string ResolveRole(string designator)
    {
        if (RoleMappings.TryGetValue(designator, out var role) && !string.IsNullOrWhiteSpace(role))
        {
            return role.Trim().ToLowerInvariant();
        }

        return string.IsNullOrWhiteSpace(DefaultRole) ? "harness" : DefaultRole.Trim().ToLowerInvariant();
    }
}
