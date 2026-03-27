// Provides Text Ct3xx File Document for the program parser document model support.
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

    /// <summary>
    /// Gets the lines.
    /// </summary>
    public IReadOnlyList<string> Lines { get; }
    /// <summary>
    /// Gets the raw text.
    /// </summary>
    public string RawText => string.Join(Environment.NewLine, Lines);
}
