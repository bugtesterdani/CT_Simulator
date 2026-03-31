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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
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
    /// <summary>
    /// Executes CreateDocument.
    /// </summary>
    protected override RepairSummaryDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}
