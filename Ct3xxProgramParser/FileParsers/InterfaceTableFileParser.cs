using System.Collections.Generic;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

public sealed class InterfaceTableFileParser : TextFileParser<InterfaceTableDocument>
{
    public override string Extension => ".ctifc";

    protected override InterfaceTableDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}
