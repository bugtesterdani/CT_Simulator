// Provides I Ct3xx File Parser for the program parser file parsing support.
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

/// <summary>
/// Defines the i ct3xx file parser contract.
/// </summary>
public interface ICt3xxFileParser
{
    string Extension { get; }
    bool CanParse(string filePath);
    Ct3xxFileDocument Parse(string filePath, Table? tableDefinition = null);
}
