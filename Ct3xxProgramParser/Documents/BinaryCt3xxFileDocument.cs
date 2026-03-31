// Provides Binary Ct3xx File Document for the program parser document model support.
using System;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public abstract class BinaryCt3xxFileDocument : Ct3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of BinaryCt3xxFileDocument.
    /// </summary>
    protected BinaryCt3xxFileDocument(string filePath, Table? tableDefinition, byte[] content)
        : base(filePath, tableDefinition)
    {
        Content = content ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Gets the content.
    /// </summary>
    public byte[] Content { get; }
    /// <summary>
    /// Gets the length.
    /// </summary>
    public long Length => Content.LongLength;
}
