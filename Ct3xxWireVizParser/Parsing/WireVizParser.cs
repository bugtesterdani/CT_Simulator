using System;
using System.IO;
using Ct3xxWireVizParser.Model;
using YamlDotNet.Serialization;

namespace Ct3xxWireVizParser.Parsing;

public sealed class WireVizParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public WireVizDocument ParseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"WireViz file '{filePath}' was not found.", filePath);
        }

        using var reader = File.OpenText(filePath);
        return Parse(reader, filePath);
    }

    public WireVizDocument Parse(string yaml, string? sourcePath = null)
    {
        if (yaml == null)
        {
            throw new ArgumentNullException(nameof(yaml));
        }

        using var reader = new StringReader(yaml);
        return Parse(reader, sourcePath);
    }

    public WireVizDocument Parse(TextReader reader, string? sourcePath = null)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var raw = _deserializer.Deserialize(reader);
        var root = WireVizValue.FromObject(raw);
        if (root.Kind != WireVizValueKind.Mapping)
        {
            throw new InvalidDataException($"[{sourcePath ?? "<input>"}] WireViz YAML root must be a mapping.");
        }

        return new WireVizDocument(sourcePath, root);
    }
}
