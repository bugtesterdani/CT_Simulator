using System;
using System.Collections.Generic;

namespace Ct3xxWireVizParser.Model;

public sealed class WireVizDocument
{
    private static readonly IReadOnlyDictionary<string, WireVizValue> EmptyMap =
        new Dictionary<string, WireVizValue>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<WireVizValue> EmptyList = Array.Empty<WireVizValue>();

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

    public string? SourcePath { get; }
    public WireVizValue Root { get; }
    public IReadOnlyDictionary<string, WireVizValue> Sections => Root.Properties;

    public WireVizValue? Metadata => GetSection("metadata");
    public WireVizValue? Options => GetSection("options");
    public WireVizValue? Tweak => GetSection("tweak");
    public WireVizValue? ConnectorsSection => GetSection("connectors");
    public WireVizValue? CablesSection => GetSection("cables");
    public WireVizValue? BundlesSection => GetSection("bundles");
    public WireVizValue? ConnectionsSection => GetSection("connections");
    public WireVizValue? AdditionalBomItemsSection => GetSection("additional_bom_items");

    public IReadOnlyDictionary<string, WireVizValue> Connectors => ConnectorsSection?.AsMappingOrEmpty() ?? EmptyMap;
    public IReadOnlyDictionary<string, WireVizConnectorDefinition> ConnectorDefinitions => BuildConnectorDefinitions();
    public IReadOnlyDictionary<string, WireVizValue> Cables => CablesSection?.AsMappingOrEmpty() ?? EmptyMap;
    public IReadOnlyDictionary<string, WireVizValue> Bundles => BundlesSection?.AsMappingOrEmpty() ?? EmptyMap;
    public IReadOnlyList<WireVizValue> Connections => ConnectionsSection?.AsSequenceOrEmpty() ?? EmptyList;
    public IReadOnlyList<WireVizValue> AdditionalBomItems => AdditionalBomItemsSection?.AsSequenceOrEmpty() ?? EmptyList;

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
