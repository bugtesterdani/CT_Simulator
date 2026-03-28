namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Represents one matched pair of simulator-visible program step and imported CSV row.
/// </summary>
public sealed class ImportedTestRunStepMatch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportedTestRunStepMatch"/> class.
    /// </summary>
    public ImportedTestRunStepMatch(
        ProgramVisibleStep programStep,
        ImportedTestRunStep csvStep,
        int csvRelevantIndex,
        double score,
        string reason)
    {
        ProgramStep = programStep;
        CsvStep = csvStep;
        CsvRelevantIndex = csvRelevantIndex;
        Score = score;
        Reason = reason;
    }

    /// <summary>
    /// Gets the matched simulator-visible step.
    /// </summary>
    public ProgramVisibleStep ProgramStep { get; }

    /// <summary>
    /// Gets the matched imported CSV step.
    /// </summary>
    public ImportedTestRunStep CsvStep { get; }

    /// <summary>
    /// Gets the zero-based index within the filtered relevant CSV rows.
    /// </summary>
    public int CsvRelevantIndex { get; }

    /// <summary>
    /// Gets the similarity score of the match.
    /// </summary>
    public double Score { get; }

    /// <summary>
    /// Gets the textual explanation of the match quality.
    /// </summary>
    public string Reason { get; }
}
