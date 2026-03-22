using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

public sealed class CadBoardDocument : TextCt3xxFileDocument
{
    public CadBoardDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class AlgorithmDocument : TextCt3xxFileDocument
{
    public AlgorithmDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class ArbitraryWaveformDocument : TextCt3xxFileDocument
{
    public ArbitraryWaveformDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class CookbookDocument : TextCt3xxFileDocument
{
    public CookbookDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class DigitalPatternDocument : TextCt3xxFileDocument
{
    public DigitalPatternDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class FixtureLoopDocument : TextCt3xxFileDocument
{
    public FixtureLoopDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class IctDocument : TextCt3xxFileDocument
{
    public IctDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class SheetDocument : TextCt3xxFileDocument
{
    public SheetDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class RepairLogDocument : TextCt3xxFileDocument
{
    public RepairLogDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

public sealed class RepairSummaryDocument : TextCt3xxFileDocument
{
    public RepairSummaryDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}
