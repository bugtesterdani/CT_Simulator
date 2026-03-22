using System.IO;
using System.Xml.Serialization;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Parsing;

public class Ct3xxProgramLoader
{
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
