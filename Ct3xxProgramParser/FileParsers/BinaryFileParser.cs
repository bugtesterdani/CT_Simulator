// Provides Binary File Parser for the program parser file parsing support.
using System;
using System.IO;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

public abstract class BinaryFileParser<TDocument> : ICt3xxFileParser where TDocument : BinaryCt3xxFileDocument
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public abstract string Extension { get; }

    /// <summary>
    /// Determines whether the parse condition is met.
    /// </summary>
    public bool CanParse(string filePath) =>
        !string.IsNullOrWhiteSpace(filePath) &&
        filePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes parse.
    /// </summary>
    public Ct3xxFileDocument Parse(string filePath, Table? tableDefinition = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File '{filePath}' was not found.", filePath);
        }

        var content = File.ReadAllBytes(filePath);
        return CreateDocument(filePath, tableDefinition, content);
    }

    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
    protected abstract TDocument CreateDocument(string filePath, Table? tableDefinition, byte[] content);
}
