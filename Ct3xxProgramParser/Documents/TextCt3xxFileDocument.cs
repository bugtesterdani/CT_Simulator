using System;
using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public abstract class TextCt3xxFileDocument : Ct3xxFileDocument
{
    protected TextCt3xxFileDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition)
    {
        Lines = lines ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Lines { get; }
    public string RawText => string.Join(Environment.NewLine, Lines);
}
