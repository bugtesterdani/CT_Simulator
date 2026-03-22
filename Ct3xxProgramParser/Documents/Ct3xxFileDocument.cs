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

    public string FilePath { get; }
    public string FileName { get; }
    public string Extension => Path.GetExtension(FilePath);
    public Table? TableDefinition { get; }
}
