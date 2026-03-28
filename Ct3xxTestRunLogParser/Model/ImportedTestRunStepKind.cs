namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Classifies one imported CSV row into a coarse semantic step kind.
/// </summary>
public enum ImportedTestRunStepKind
{
    /// <summary>
    /// The row could not be classified reliably.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The row contains informational output rather than a measurable test step.
    /// </summary>
    Information = 1,

    /// <summary>
    /// The row represents a generic executed test step.
    /// </summary>
    Step = 2,

    /// <summary>
    /// The row represents a measured value with optional lower and upper limits.
    /// </summary>
    Measurement = 3,

    /// <summary>
    /// The row represents a wait or reset delay.
    /// </summary>
    Wait = 4,

    /// <summary>
    /// The row represents a script, command, assignment or other operational action.
    /// </summary>
    Action = 5,
}
