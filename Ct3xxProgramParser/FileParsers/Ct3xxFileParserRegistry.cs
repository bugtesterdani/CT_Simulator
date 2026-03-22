using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.Parsing;

namespace Ct3xxProgramParser.FileParsers;

public sealed class Ct3xxFileParserRegistry
{
    private readonly List<ICt3xxFileParser> _parsers = new();

    public Ct3xxFileParserRegistry Add(ICt3xxFileParser parser)
    {
        if (parser == null)
        {
            throw new ArgumentNullException(nameof(parser));
        }

        _parsers.Add(parser);
        return this;
    }

    public ICt3xxFileParser? FindParser(string filePath)
    {
        return _parsers.FirstOrDefault(parser => parser.CanParse(filePath));
    }

    public IReadOnlyList<ICt3xxFileParser> Parsers => _parsers;

    public static Ct3xxFileParserRegistry CreateDefault()
    {
        var registry = new Ct3xxFileParserRegistry();
        registry
            .Add(new SignalTableParser())
            .Add(new InterfaceTableFileParser())
            .Add(new CadBoardFileParser())
            .Add(new AlgorithmFileParser())
            .Add(new ArbitraryWaveformFileParser())
            .Add(new CookbookFileParser())
            .Add(new DigitalPatternFileParser())
            .Add(new FixtureLoopFileParser())
            .Add(new IctFileParser())
            .Add(new SheetFileParser())
            .Add(new RepairLogFileParser())
            .Add(new RepairSummaryFileParser());

        return registry;
    }
}
