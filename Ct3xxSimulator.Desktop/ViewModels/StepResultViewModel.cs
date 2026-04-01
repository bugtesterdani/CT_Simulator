// Provides Step Result View Model for the desktop application view model support.
using System;
using System.Collections.Generic;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.ViewModels;

/// <summary>
/// Represents the step result view model.
/// </summary>
public sealed class StepResultViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepResultViewModel"/> class.
    /// </summary>
    public StepResultViewModel(
        string stepName,
        string outcome,
        string measuredValue,
        string lowerLimit,
        string upperLimit,
        string unit,
        string details,
        IReadOnlyList<StepConnectionTrace>? traces = null,
        IReadOnlyList<MeasurementCurvePoint>? curvePoints = null,
        int? timelineIndex = null,
        IReadOnlyDictionary<string, string>? variables = null,
        int? csvRowNumber = null,
        string? csvDescription = null,
        string? csvMessage = null,
        string? csvOutcome = null,
        string? csvMeasuredValue = null,
        string? csvLowerLimit = null,
        string? csvUpperLimit = null,
        string? csvMatchReason = null,
        string? csvDisplayMode = null,
        string? csvLogFlags = null,
        bool csvLogExpectedForOutcome = false)
    {
        StepName = stepName;
        Outcome = outcome;
        MeasuredValue = measuredValue;
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        Unit = unit;
        Details = details;
        Traces = traces ?? Array.Empty<StepConnectionTrace>();
        CurvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
        TimelineIndex = timelineIndex;
        Variables = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CsvRowNumber = csvRowNumber;
        CsvDescription = csvDescription ?? string.Empty;
        CsvMessage = csvMessage ?? string.Empty;
        CsvOutcome = csvOutcome ?? string.Empty;
        CsvMeasuredValue = csvMeasuredValue ?? string.Empty;
        CsvLowerLimit = csvLowerLimit ?? string.Empty;
        CsvUpperLimit = csvUpperLimit ?? string.Empty;
        CsvMatchReason = csvMatchReason ?? string.Empty;
        CsvDisplayMode = csvDisplayMode ?? "Off";
        CsvLogFlags = csvLogFlags ?? string.Empty;
        CsvLogExpectedForOutcome = csvLogExpectedForOutcome;
    }

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public string StepName { get; }
    /// <summary>
    /// Gets the simulator outcome.
    /// </summary>
    public string Outcome { get; }
    /// <summary>
    /// Gets the simulator measured value.
    /// </summary>
    public string MeasuredValue { get; }
    /// <summary>
    /// Gets the simulator lower limit.
    /// </summary>
    public string LowerLimit { get; }
    /// <summary>
    /// Gets the simulator upper limit.
    /// </summary>
    public string UpperLimit { get; }
    /// <summary>
    /// Gets the simulator unit.
    /// </summary>
    public string Unit { get; }
    /// <summary>
    /// Gets the simulator details.
    /// </summary>
    public string Details { get; }
    /// <summary>
    /// Gets the traces.
    /// </summary>
    public IReadOnlyList<StepConnectionTrace> Traces { get; }
    /// <summary>
    /// Gets the curve points.
    /// </summary>
    public IReadOnlyList<MeasurementCurvePoint> CurvePoints { get; }
    /// <summary>
    /// Gets the timeline index.
    /// </summary>
    public int? TimelineIndex { get; }
    /// <summary>
    /// Gets the variables captured for the step snapshot.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; }
    /// <summary>
    /// Gets the matched CSV row number, if any.
    /// </summary>
    public int? CsvRowNumber { get; }
    /// <summary>
    /// Gets the matched CSV description, if any.
    /// </summary>
    public string CsvDescription { get; }
    /// <summary>
    /// Gets the matched CSV message, if any.
    /// </summary>
    public string CsvMessage { get; }
    /// <summary>
    /// Gets the matched CSV outcome, if any.
    /// </summary>
    public string CsvOutcome { get; }
    /// <summary>
    /// Gets the matched CSV measured value, if any.
    /// </summary>
    public string CsvMeasuredValue { get; }
    /// <summary>
    /// Gets the matched CSV lower limit, if any.
    /// </summary>
    public string CsvLowerLimit { get; }
    /// <summary>
    /// Gets the matched CSV upper limit, if any.
    /// </summary>
    public string CsvUpperLimit { get; }
    /// <summary>
    /// Gets the textual reason for the CSV match, if any.
    /// </summary>
    public string CsvMatchReason { get; }
    /// <summary>
    /// Gets the configured display mode of the result.
    /// </summary>
    public string CsvDisplayMode { get; }
    /// <summary>
    /// Gets the originating CT3xx log flags for the underlying test.
    /// </summary>
    public string CsvLogFlags { get; }
    /// <summary>
    /// Gets a value indicating whether the current simulator outcome should be present in the historical CSV according to <see cref="CsvLogFlags"/>.
    /// </summary>
    public bool CsvLogExpectedForOutcome { get; }
    /// <summary>
    /// Gets a value indicating whether any CSV replay mode is active.
    /// </summary>
    public bool HasActiveCsvReplay =>
        !string.Equals(CsvDisplayMode, "Off", StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Gets a value indicating whether the step has a matched CSV entry.
    /// </summary>
    public bool HasCsvReplayMatch => CsvRowNumber.HasValue;
    /// <summary>
    /// Gets a value indicating whether the step has no CSV row although CSV replay is active.
    /// </summary>
    public bool IsUnloggedInActiveCsvReplay => HasActiveCsvReplay && !HasCsvReplayMatch && !CsvLogExpectedForOutcome;
    /// <summary>
    /// Gets a value indicating whether a CSV row is missing although the configured log flags require one for the current outcome.
    /// </summary>
    public bool IsMissingExpectedCsvReplayEntry => HasActiveCsvReplay && !HasCsvReplayMatch && CsvLogExpectedForOutcome;
    /// <summary>
    /// Gets a value indicating whether the result is currently displayed from the CSV side.
    /// </summary>
    public bool UsesCsvAsPrimary =>
        HasCsvReplayMatch &&
        string.Equals(CsvDisplayMode, "CsvDrivesResult", StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Gets the currently displayed outcome.
    /// </summary>
    public string DisplayOutcome => UsesCsvAsPrimary && !string.IsNullOrWhiteSpace(CsvOutcome) ? CsvOutcome : Outcome;
    /// <summary>
    /// Gets the currently displayed measured value.
    /// </summary>
    public string DisplayMeasuredValue => UsesCsvAsPrimary && !string.IsNullOrWhiteSpace(CsvMeasuredValue) ? CsvMeasuredValue : MeasuredValue;
    /// <summary>
    /// Gets the currently displayed lower limit.
    /// </summary>
    public string DisplayLowerLimit => UsesCsvAsPrimary && !string.IsNullOrWhiteSpace(CsvLowerLimit) ? CsvLowerLimit : LowerLimit;
    /// <summary>
    /// Gets the currently displayed upper limit.
    /// </summary>
    public string DisplayUpperLimit => UsesCsvAsPrimary && !string.IsNullOrWhiteSpace(CsvUpperLimit) ? CsvUpperLimit : UpperLimit;
    /// <summary>
    /// Gets the currently displayed unit.
    /// </summary>
    public string DisplayUnit => UsesCsvAsPrimary ? string.Empty : Unit;
    /// <summary>
    /// Gets the currently displayed details text.
    /// </summary>
    public string DisplayDetails => UsesCsvAsPrimary ? BuildCsvDetails() : Details;
    /// <summary>
    /// Gets the result source label shown in the UI.
    /// </summary>
    public string ResultSourceLabel =>
        !HasCsvReplayMatch ? "SIM" :
        string.Equals(CsvDisplayMode, "Compare", StringComparison.OrdinalIgnoreCase) ? "SIM/CSV" :
        string.Equals(CsvDisplayMode, "CsvDrivesResult", StringComparison.OrdinalIgnoreCase) ? "CSV" :
        "SIM";
    /// <summary>
    /// Gets a short SIM-vs-CSV comparison summary.
    /// </summary>
    public string ComparisonSummary =>
        IsMissingExpectedCsvReplayEntry
            ? $"CSV-Eintrag fehlt fuer Ergebnis {Outcome}. LogFlags '{CsvLogFlags}' erwarten hier einen Logeintrag."
            : IsUnloggedInActiveCsvReplay
            ? "Kein CSV-Eintrag fuer diesen Schritt"
            : !HasCsvReplayMatch
            ? string.Empty
            : $"SIM {Outcome} | CSV {CsvOutcome}{(string.IsNullOrWhiteSpace(CsvMatchReason) ? string.Empty : $" | Match: {CsvMatchReason}")}";

    /// <summary>
    /// Gets a value indicating whether the result has a visible CSV comparison line.
    /// </summary>
    public bool HasComparisonSummary =>
        (HasCsvReplayMatch &&
        (string.Equals(CsvDisplayMode, "Compare", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CsvDisplayMode, "CsvDrivesResult", StringComparison.OrdinalIgnoreCase))) ||
        IsMissingExpectedCsvReplayEntry ||
        IsUnloggedInActiveCsvReplay;
    /// <summary>
    /// Gets the status badge background color.
    /// </summary>
    public string OutcomeBadgeBackground =>
        IsMissingExpectedCsvReplayEntry ? "#FFF8E8EA" :
        IsUnloggedInActiveCsvReplay ? "#FFE3E5E8" :
        DisplayOutcome.Equals("PASS", StringComparison.OrdinalIgnoreCase) ? "#FFEAF6EE" :
        DisplayOutcome.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ? "#FFFDEDEE" :
        DisplayOutcome.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? "#FFF8E8EA" :
        "#FFEAF1F7";
    /// <summary>
    /// Gets the status badge foreground color.
    /// </summary>
    public string OutcomeBadgeForeground =>
        IsMissingExpectedCsvReplayEntry ? "#FF8B1E3F" :
        IsUnloggedInActiveCsvReplay ? "#FF4B5563" :
        DisplayOutcome.Equals("PASS", StringComparison.OrdinalIgnoreCase) ? "#FF2D6A4F" :
        DisplayOutcome.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ? "#FF8B1E3F" :
        DisplayOutcome.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? "#FF8B1E3F" :
        "#FF2E5E86";

    /// <summary>
    /// Executes BuildCsvDetails.
    /// </summary>
    private string BuildCsvDetails()
    {
        if (string.IsNullOrWhiteSpace(CsvDescription) && string.IsNullOrWhiteSpace(CsvMessage))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(CsvMessage))
        {
            return CsvDescription;
        }

        if (string.IsNullOrWhiteSpace(CsvDescription) || string.Equals(CsvDescription, StepName, StringComparison.OrdinalIgnoreCase))
        {
            return CsvMessage;
        }

        return CsvDescription + ": " + CsvMessage;
    }
}
