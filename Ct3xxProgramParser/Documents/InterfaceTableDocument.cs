// Provides Interface Table Document for the program parser document model support.
using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

/// <summary>
/// Represents the interface table document.
/// </summary>
public sealed class InterfaceTableDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InterfaceTableDocument"/> class.
    /// </summary>
    public InterfaceTableDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines)
    {
    }
}
