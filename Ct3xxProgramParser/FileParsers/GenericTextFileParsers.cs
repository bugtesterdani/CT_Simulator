using System.Collections.Generic;
using Ct3xxProgramParser.Documents;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.FileParsers;

public sealed class CadBoardFileParser : TextFileParser<CadBoardDocument>
{
    public override string Extension => ".ctbrd";
    protected override CadBoardDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class AlgorithmFileParser : TextFileParser<AlgorithmDocument>
{
    public override string Extension => ".ctalg";
    protected override AlgorithmDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class ArbitraryWaveformFileParser : TextFileParser<ArbitraryWaveformDocument>
{
    public override string Extension => ".ctarb";
    protected override ArbitraryWaveformDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class CookbookFileParser : TextFileParser<CookbookDocument>
{
    public override string Extension => ".ctcok";
    protected override CookbookDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class DigitalPatternFileParser : TextFileParser<DigitalPatternDocument>
{
    public override string Extension => ".ctdig";
    protected override DigitalPatternDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class FixtureLoopFileParser : TextFileParser<FixtureLoopDocument>
{
    public override string Extension => ".ctflc";
    protected override FixtureLoopDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class IctFileParser : TextFileParser<IctDocument>
{
    public override string Extension => ".ctict";
    protected override IctDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class SheetFileParser : TextFileParser<SheetDocument>
{
    public override string Extension => ".ctsht";
    protected override SheetDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class RepairLogFileParser : TextFileParser<RepairLogDocument>
{
    public override string Extension => ".ctrlg";
    protected override RepairLogDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}

public sealed class RepairSummaryFileParser : TextFileParser<RepairSummaryDocument>
{
    public override string Extension => ".ctrsf";
    protected override RepairSummaryDocument CreateDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines) =>
        new(filePath, tableDefinition, lines);
}
