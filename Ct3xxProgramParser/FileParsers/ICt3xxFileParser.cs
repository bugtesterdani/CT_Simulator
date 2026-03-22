using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

public interface ICt3xxFileParser
{
    string Extension { get; }
    bool CanParse(string filePath);
    Ct3xxFileDocument Parse(string filePath, Table? tableDefinition = null);
}
