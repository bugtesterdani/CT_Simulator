// Provides Ct3xx File Document for the program parser document model support.
using System;
using System.IO;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public abstract class Ct3xxFileDocument
{
    protected Ct3xxFileDocument(string filePath, Table? tableDefinition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        FilePath = Path.GetFullPath(filePath);
        FileName = Path.GetFileName(FilePath) ?? FilePath;
        TableDefinition = tableDefinition;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }
    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string FileName { get; }
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public string Extension => Path.GetExtension(FilePath);
    /// <summary>
    /// Gets the table definition.
    /// </summary>
    public Table? TableDefinition { get; }
}
