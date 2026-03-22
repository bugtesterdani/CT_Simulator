using System;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public abstract class BinaryCt3xxFileDocument : Ct3xxFileDocument
{
    protected BinaryCt3xxFileDocument(string filePath, Table? tableDefinition, byte[] content)
        : base(filePath, tableDefinition)
    {
        Content = content ?? Array.Empty<byte>();
    }

    public byte[] Content { get; }
    public long Length => Content.LongLength;
}
