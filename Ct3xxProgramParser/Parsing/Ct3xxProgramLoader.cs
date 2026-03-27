// Provides Ct3xx Program Loader for the program parser parsing support.
using System.IO;
using System.Xml.Serialization;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Parsing;

/// <summary>
/// Represents the ct3xx program loader.
/// </summary>
public class Ct3xxProgramLoader
{
    /// <summary>
    /// Executes load.
    /// </summary>
    public Ct3xxProgram Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot find CT3xx program at '{filePath}'.", filePath);
        }

        var serializer = new XmlSerializer(typeof(Ct3xxProgram));
        using var stream = File.OpenRead(filePath);
        return (Ct3xxProgram)(serializer.Deserialize(stream) ?? throw new InvalidDataException("Program file is empty."));
    }
}
