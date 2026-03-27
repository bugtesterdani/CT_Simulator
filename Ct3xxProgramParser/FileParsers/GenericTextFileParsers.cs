// Provides Generic Text File Parsers for the program parser file parsing support.
using System.Collections.Generic;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

/// <summary>
/// Represents the cad board file parser.
/// </summary>
public sealed class CadBoardFileParser : TextFileParser<CadBoardDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctbrd";
    protected override CadBoardDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the algorithm file parser.
/// </summary>
public sealed class AlgorithmFileParser : TextFileParser<AlgorithmDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctalg";
    protected override AlgorithmDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the arbitrary waveform file parser.
/// </summary>
public sealed class ArbitraryWaveformFileParser : TextFileParser<ArbitraryWaveformDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctarb";
    protected override ArbitraryWaveformDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the cookbook file parser.
/// </summary>
public sealed class CookbookFileParser : TextFileParser<CookbookDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctcok";
    protected override CookbookDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the digital pattern file parser.
/// </summary>
public sealed class DigitalPatternFileParser : TextFileParser<DigitalPatternDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctdig";
    protected override DigitalPatternDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the fixture loop file parser.
/// </summary>
public sealed class FixtureLoopFileParser : TextFileParser<FixtureLoopDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctflc";
    protected override FixtureLoopDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the ict file parser.
/// </summary>
public sealed class IctFileParser : TextFileParser<IctDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctict";
    protected override IctDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the sheet file parser.
/// </summary>
public sealed class SheetFileParser : TextFileParser<SheetDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctsht";
    protected override SheetDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the repair log file parser.
/// </summary>
public sealed class RepairLogFileParser : TextFileParser<RepairLogDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctrlg";
    protected override RepairLogDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

/// <summary>
/// Represents the repair summary file parser.
/// </summary>
public sealed class RepairSummaryFileParser : TextFileParser<RepairSummaryDocument>
{
    /// <summary>
    /// Gets the extension.
    /// </summary>
    public override string Extension => ".ctrsf";
    protected override RepairSummaryDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}
