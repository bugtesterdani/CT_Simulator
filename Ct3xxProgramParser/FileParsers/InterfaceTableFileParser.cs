// Provides Interface Table File Parser for the program parser file parsing support.
using System.Collections.Generic;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

/// <summary>
/// Represents the interface table file parser.
/// </summary>
public sealed class InterfaceTableFileParser : TextFileParser<InterfaceTableDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctifc";

    protected override InterfaceTableDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}
