using System.Globalization;

namespace Ct3xxTestRunLogParser.Model;

/// <summary>
/// Represents one imported CSV log row from a historical CT3xx test run.
/// </summary>
public sealed class ImportedTestRunStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportedTestRunStep"/> class.
    /// </summary>
    public ImportedTestRunStep(
        int rowNumber,
        string? runId,
        string? testTime,
        string? serialNumber,
        string description,
        string? message,
        string? rawLowerLimit,
        string? rawMeasuredValue,
        string? rawUpperLimit,
        string result,
        ImportedTestRunStepKind kind,
        double? lowerLimit,
        double? measuredValue,
        double? upperLimit)
    {
        RowNumber = rowNumber;
        RunId = runId;
        TestTime = testTime;
        SerialNumber = serialNumber;
        Description = description;
        Message = message;
        RawLowerLimit = rawLowerLimit;
        RawMeasuredValue = rawMeasuredValue;
        RawUpperLimit = rawUpperLimit;
        Result = result;
        Kind = kind;
        LowerLimit = lowerLimit;
        MeasuredValue = measuredValue;
        UpperLimit = upperLimit;
    }

    /// <summary>
    /// Gets the original CSV row number.
    /// </summary>
    public int RowNumber { get; }

    /// <summary>
    /// Gets the imported run identifier.
    /// </summary>
    public string? RunId { get; }

    /// <summary>
    /// Gets the imported test time text.
    /// </summary>
    public string? TestTime { get; }

    /// <summary>
    /// Gets the imported serial number.
    /// </summary>
    public string? SerialNumber { get; }

    /// <summary>
    /// Gets the imported step description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the imported message column.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the raw lower-limit text.
    /// </summary>
    public string? RawLowerLimit { get; }

    /// <summary>
    /// Gets the raw measured-value text.
    /// </summary>
    public string? RawMeasuredValue { get; }

    /// <summary>
    /// Gets the raw upper-limit text.
    /// </summary>
    public string? RawUpperLimit { get; }

    /// <summary>
    /// Gets the imported result text.
    /// </summary>
    public string Result { get; }

    /// <summary>
    /// Gets the coarse step kind.
    /// </summary>
    public ImportedTestRunStepKind Kind { get; }

    /// <summary>
    /// Gets the parsed lower limit when numeric.
    /// </summary>
    public double? LowerLimit { get; }

    /// <summary>
    /// Gets the parsed measured value when numeric.
    /// </summary>
    public double? MeasuredValue { get; }

    /// <summary>
    /// Gets the parsed upper limit when numeric.
    /// </summary>
    public double? UpperLimit { get; }

    /// <summary>
    /// Gets a value indicating whether this row contains at least one numeric limit or value.
    /// </summary>
    public bool HasNumericContent => LowerLimit.HasValue || MeasuredValue.HasValue || UpperLimit.HasValue;

    /// <summary>
    /// Gets a value indicating whether this row reports a failing result.
    /// </summary>
    public bool IsFailLike =>
        string.Equals(Result, "FAIL", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Result, "ERROR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a compact debug string for logs and diagnostics.
    /// </summary>
    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0} {1} [{2}]",
            RowNumber,
            Description,
            Result);
    }
}
