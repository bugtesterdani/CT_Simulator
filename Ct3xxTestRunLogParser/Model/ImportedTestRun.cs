namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Represents one parsed CSV-backed historical test run including metadata and step rows.
/// </summary>
public sealed class ImportedTestRun
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportedTestRun"/> class.
    /// </summary>
    public ImportedTestRun(
        string sourcePath,
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportedTestRunStep> steps,
        string? runId,
        string? serialNumber)
    {
        SourcePath = sourcePath;
        Headers = headers;
        Steps = steps;
        RunId = runId;
        SerialNumber = serialNumber;
    }

    /// <summary>
    /// Gets the original CSV source path.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the validated header row.
    /// </summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>
    /// Gets all imported CSV rows as typed steps.
    /// </summary>
    public IReadOnlyList<ImportedTestRunStep> Steps { get; }

    /// <summary>
    /// Gets the run identifier discovered in the file.
    /// </summary>
    public string? RunId { get; }

    /// <summary>
    /// Gets the serial number discovered in the file.
    /// </summary>
    public string? SerialNumber { get; }
}
