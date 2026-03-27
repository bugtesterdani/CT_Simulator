// Provides Text Document Types for the program parser document model support.
using System.Collections.Generic;
using Ct3xxProgramParser.Model;

namespace Ct3xxProgramParser.Documents;

/// <summary>
/// Represents the cad board document.
/// </summary>
public sealed class CadBoardDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadBoardDocument"/> class.
    /// </summary>
    public CadBoardDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the algorithm document.
/// </summary>
public sealed class AlgorithmDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmDocument"/> class.
    /// </summary>
    public AlgorithmDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the arbitrary waveform document.
/// </summary>
public sealed class ArbitraryWaveformDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArbitraryWaveformDocument"/> class.
    /// </summary>
    public ArbitraryWaveformDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the cookbook document.
/// </summary>
public sealed class CookbookDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CookbookDocument"/> class.
    /// </summary>
    public CookbookDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the digital pattern document.
/// </summary>
public sealed class DigitalPatternDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DigitalPatternDocument"/> class.
    /// </summary>
    public DigitalPatternDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the fixture loop document.
/// </summary>
public sealed class FixtureLoopDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FixtureLoopDocument"/> class.
    /// </summary>
    public FixtureLoopDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the ict document.
/// </summary>
public sealed class IctDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IctDocument"/> class.
    /// </summary>
    public IctDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the sheet document.
/// </summary>
public sealed class SheetDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SheetDocument"/> class.
    /// </summary>
    public SheetDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the repair log document.
/// </summary>
public sealed class RepairLogDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepairLogDocument"/> class.
    /// </summary>
    public RepairLogDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}

/// <summary>
/// Represents the repair summary document.
/// </summary>
public sealed class RepairSummaryDocument : TextCt3xxFileDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepairSummaryDocument"/> class.
    /// </summary>
    public RepairSummaryDocument(string filePath, Table? tableDefinition, IReadOnlyList<string> lines)
        : base(filePath, tableDefinition, lines) { }
}
