// Provides Ct3xx File Parser Registry for the program parser file parsing support.
using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.Parsing;

namespace Ct3xxProgramParser.FileParsers;

/// <summary>
/// Represents the ct3xx file parser registry.
/// </summary>
public sealed class Ct3xxFileParserRegistry
{
    private readonly List<ICt3xxFileParser> _parsers = new();

    /// <summary>
    /// Executes add.
    /// </summary>
    public Ct3xxFileParserRegistry Add(ICt3xxFileParser parser)
    {
        if (parser == null)
        {
            throw new ArgumentNullException(nameof(parser));
        }

        _parsers.Add(parser);
        return this;
    }

    /// <summary>
    /// Finds the parser.
    /// </summary>
    public ICt3xxFileParser? FindParser(string filePath)
    {
        return _parsers.FirstOrDefault(parser => parser.CanParse(filePath));
    }

    /// <summary>
    /// Gets the parsers.
    /// </summary>
    public IReadOnlyList<ICt3xxFileParser> Parsers => _parsers;

    /// <summary>
    /// Creates the default.
    /// </summary>
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
