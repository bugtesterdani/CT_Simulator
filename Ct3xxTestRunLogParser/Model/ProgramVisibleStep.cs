using Ct3xxProgramParser.Model;

namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Represents one visible simulator step that can later be matched against a CSV result row.
/// </summary>
public sealed class ProgramVisibleStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgramVisibleStep"/> class.
    /// </summary>
    public ProgramVisibleStep(
        int sequenceIndex,
        string displayName,
        string nodePath,
        string? testId,
        string? testName,
        string? groupPath,
        Test sourceTest,
        bool isSyntheticEvaluation)
    {
        SequenceIndex = sequenceIndex;
        DisplayName = displayName;
        NodePath = nodePath;
        TestId = testId;
        TestName = testName;
        GroupPath = groupPath;
        SourceTest = sourceTest;
        IsSyntheticEvaluation = isSyntheticEvaluation;
    }

    /// <summary>
    /// Gets the zero-based execution order of the visible step.
    /// </summary>
    public int SequenceIndex { get; }

    /// <summary>
    /// Gets the display name that is also used for simulator result presentation.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the stable logical node path within the parsed CT3xx program tree.
    /// </summary>
    public string NodePath { get; }

    /// <summary>
    /// Gets the CT3xx test identifier.
    /// </summary>
    public string? TestId { get; }

    /// <summary>
    /// Gets the original CT3xx test name.
    /// </summary>
    public string? TestName { get; }

    /// <summary>
    /// Gets the readable parent group path for diagnostics.
    /// </summary>
    public string? GroupPath { get; }

    /// <summary>
    /// Gets the original parsed test node.
    /// </summary>
    public Test SourceTest { get; }

    /// <summary>
    /// Gets a value indicating whether this visible step is a synthetic sub-evaluation of one test.
    /// </summary>
    public bool IsSyntheticEvaluation { get; }
}
