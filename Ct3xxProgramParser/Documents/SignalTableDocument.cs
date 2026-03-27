// Provides Signal Table Document for the program parser document model support.
using System;
using Ct3xxProgramParser.Model;
using Ct3xxProgramParser.SignalTables;

namespace Ct3xxProgramParser.Documents;

/// <summary>
/// Represents the signal table document.
/// </summary>
public sealed class SignalTableDocument : Ct3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignalTableDocument"/> class.
    /// </summary>
    public SignalTableDocument(string filePath, Table? tableDefinition, SignalTable table)
        : base(filePath, tableDefinition)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }

    /// <summary>
    /// Gets the table.
    /// </summary>
    public SignalTable Table { get; }
}
