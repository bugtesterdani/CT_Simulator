using System;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.SignalTables;

namespace Ct3xxProgramParser.Documents;

public sealed class SignalTableDocument : Ct3xxFileDocument
{
    public SignalTableDocument(string filePath, Table? tableDefinition, SignalTable table)
        : base(filePath, tableDefinition)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public SignalTable Table { get; }
}
