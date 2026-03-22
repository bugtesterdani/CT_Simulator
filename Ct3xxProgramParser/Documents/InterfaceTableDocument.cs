using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public sealed class InterfaceTableDocument : TextCt3xxFileDocument
{
    public InterfaceTableDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines)
    {
    }
}
