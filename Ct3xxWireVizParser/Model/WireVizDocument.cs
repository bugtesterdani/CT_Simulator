// Provides Wire Viz Document for the WireViz parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxWireVizParser.Model;

/// <summary>
/// Represents the wire viz document.
/// </summary>
public sealed class WireVizDocument
{
    private static readonly IReadOnlyDictionary<string, WireVizValue> EmptyMap =
        new Dictionary<string, WireVizValue>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<WireVizValue> EmptyList = Array.Empty<WireVizValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizDocument"/> class.
    /// </summary>
    public WireVizDocument(string? sourcePath, WireVizValue root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (root.Kind != WireVizValueKind.Mapping)
        {
            throw new ArgumentException("WireViz root document must be a YAML mapping.", nameof(root));
        }

        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        Root = root;
    }

    /// <summary>
    /// Gets the source path.
    /// </summary>
    public string? SourcePath { get; }
    /// <summary>
    /// Gets the root.
    /// </summary>
    public WireVizValue Root { get; }
    /// <summary>
    /// Gets the sections.
    /// </summary>
    public IReadOnlyDictionary<string, WireVizValue> Sections => Root.Properties;

    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? Metadata => GetSection("metadata");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? Options => GetSection("options");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? Tweak => GetSection("tweak");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? ConnectorsSection => GetSection("connectors");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? CablesSection => GetSection("cables");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? BundlesSection => GetSection("bundles");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? ConnectionsSection => GetSection("connections");
    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? AdditionalBomItemsSection => GetSection("additional_bom_items");

    /// <summary>
    /// Gets the connectors.
    /// </summary>
    public IReadOnlyDictionary<string, WireVizValue> Connectors => ConnectorsSection?.AsMappingOrEmpty() ?? EmptyMap;
    /// <summary>
    /// Builds the connector definitions.
    /// </summary>
    public IReadOnlyDictionary<string, WireVizConnectorDefinition> ConnectorDefinitions => BuildConnectorDefinitions();
    /// <summary>
    /// Gets the cables.
    /// </summary>
    public IReadOnlyDictionary<string, WireVizValue> Cables => CablesSection?.AsMappingOrEmpty() ?? EmptyMap;
    /// <summary>
    /// Gets the bundles.
    /// </summary>
    public IReadOnlyDictionary<string, WireVizValue> Bundles => BundlesSection?.AsMappingOrEmpty() ?? EmptyMap;
    /// <summary>
    /// Gets the connections.
    /// </summary>
    public IReadOnlyList<WireVizValue> Connections => ConnectionsSection?.AsSequenceOrEmpty() ?? EmptyList;
    /// <summary>
    /// Gets the additional bom items.
    /// </summary>
    public IReadOnlyList<WireVizValue> AdditionalBomItems => AdditionalBomItemsSection?.AsSequenceOrEmpty() ?? EmptyList;

    /// <summary>
    /// Gets the section.
    /// </summary>
    public WireVizValue? GetSection(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (Sections.TryGetValue(name, out var exact))
        {
            return exact;
        }

        foreach (var pair in Sections)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes BuildConnectorDefinitions.
    /// </summary>
    private IReadOnlyDictionary<string, WireVizConnectorDefinition> BuildConnectorDefinitions()
    {
        var definitions = new Dictionary<string, WireVizConnectorDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var connector in Connectors)
        {
            definitions[connector.Key] = new WireVizConnectorDefinition(connector.Key, connector.Value);
        }

        return definitions;
    }
}
