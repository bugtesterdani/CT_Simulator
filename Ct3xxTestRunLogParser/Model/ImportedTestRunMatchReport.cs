using System.Globalization;

namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Represents the result of matching one parsed CT3xx program against one imported CSV test run.
/// </summary>
public sealed class ImportedTestRunMatchReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportedTestRunMatchReport"/> class.
    /// </summary>
    public ImportedTestRunMatchReport(
        IReadOnlyList<ProgramVisibleStep> programSteps,
        IReadOnlyList<ImportedTestRunStep> relevantCsvSteps,
        IReadOnlyList<ImportedTestRunStepMatch> matches,
        IReadOnlyList<ProgramVisibleStep> unmatchedProgramSteps,
        IReadOnlyList<ImportedTestRunStep> unmatchedCsvSteps)
    {
        ProgramSteps = programSteps;
        RelevantCsvSteps = relevantCsvSteps;
        Matches = matches;
        UnmatchedProgramSteps = unmatchedProgramSteps;
        UnmatchedCsvSteps = unmatchedCsvSteps;
    }

    /// <summary>
    /// Gets all visible simulator steps that were considered for matching.
    /// </summary>
    public IReadOnlyList<ProgramVisibleStep> ProgramSteps { get; }

    /// <summary>
    /// Gets all CSV rows that were considered relevant for matching.
    /// </summary>
    public IReadOnlyList<ImportedTestRunStep> RelevantCsvSteps { get; }

    /// <summary>
    /// Gets all successful matches.
    /// </summary>
    public IReadOnlyList<ImportedTestRunStepMatch> Matches { get; }

    /// <summary>
    /// Gets simulator steps that could not be matched.
    /// </summary>
    public IReadOnlyList<ProgramVisibleStep> UnmatchedProgramSteps { get; }

    /// <summary>
    /// Gets relevant CSV rows that could not be matched.
    /// </summary>
    public IReadOnlyList<ImportedTestRunStep> UnmatchedCsvSteps { get; }

    /// <summary>
    /// Gets the ratio of matched simulator steps.
    /// </summary>
    public double ProgramCoverage =>
        ProgramSteps.Count == 0 ? 1d : (double)Matches.Count / ProgramSteps.Count;

    /// <summary>
    /// Gets the ratio of matched relevant CSV rows.
    /// </summary>
    public double CsvCoverage =>
        RelevantCsvSteps.Count == 0 ? 1d : (double)Matches.Count / RelevantCsvSteps.Count;

    /// <summary>
    /// Gets the average score of all accepted matches.
    /// </summary>
    public double AverageScore =>
        Matches.Count == 0 ? 0d : Matches.Average(item => item.Score);

    /// <summary>
    /// Gets a value indicating whether the matching result is likely reliable enough for CSV-guided replay.
    /// </summary>
    public bool IsReliable =>
        Matches.Count > 0 &&
        CsvCoverage >= 0.6d &&
        AverageScore >= 0.7d &&
        UnmatchedCsvSteps.Count <= Math.Max(1, RelevantCsvSteps.Count / 3);

    /// <summary>
    /// Builds a short diagnostics summary.
    /// </summary>
    public string Summary =>
        string.Format(
            CultureInfo.InvariantCulture,
            "ProgramSteps={0}, CsvSteps={1}, Matches={2}, ProgramCoverage={3:P0}, CsvCoverage={4:P0}, AverageScore={5:0.00}, Reliable={6}",
            ProgramSteps.Count,
            RelevantCsvSteps.Count,
            Matches.Count,
            ProgramCoverage,
            CsvCoverage,
            AverageScore,
            IsReliable ? "yes" : "no");
}
