using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes AM4 stimulus/acquisition tests.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunAm4nTest.
    /// </summary>
    private TestOutcome RunAm4nTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "AM4N ohne Parameter.");
            return TestOutcome.Error;
        }

        var rows = EnumerateAm4Rows(parameters).ToList();
        if (rows.Count == 0)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "AM4N ohne Datensaetze.");
            return TestOutcome.Error;
        }

        var traces = new List<StepConnectionTrace>();
        var details = new List<string>();
        var overallOutcome = TestOutcome.Pass;
        var signalOutputState = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.Mode.Equals("Delay", StringComparison.OrdinalIgnoreCase))
            {
                if (row.DelayMs > 0)
                {
                    AdvanceTime(row.DelayMs);
                    details.Add($"Delay {row.DelayMs} ms");
                }
                continue;
            }

            if (row.Mode.Equals("Stimulation", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(row.SignalName))
                {
                    details.Add($"Stimulus [{row.Index}] ohne Signal.");
                    overallOutcome = MergeOutcome(overallOutcome, TestOutcome.Error);
                    continue;
                }

                var targetValue = row.StateOn ? row.OutputVoltage ?? 0d : 0d;
                var startValue = signalOutputState.TryGetValue(row.SignalName, out var existing) ? existing : 0d;

                if (row.RampOn && row.RampTimeMs > 0)
                {
                    RecordCurvePoint($"AM4 Stimulus {row.SignalName}", startValue, "V");
                    AdvanceTime(row.RampTimeMs);
                }

                WriteSignal(row.SignalName, targetValue);
                RecordCurvePoint($"AM4 Stimulus {row.SignalName}", targetValue, "V");
                signalOutputState[row.SignalName] = targetValue;

                traces.AddRange(CollectSignalTraces(row.SignalName, "AM4 Stimulus"));
                var shuntText = string.IsNullOrWhiteSpace(row.Shunt) ? string.Empty : $" (Shunt {row.Shunt})";
                details.Add($"Stimulus {row.SignalName} <= {targetValue.ToString("0.###", CultureInfo.InvariantCulture)} V{shuntText}");

                if (row.SenseMin.HasValue || row.SenseMax.HasValue)
                {
                    var senseOutcome = EvaluateSenseLimits(row, targetValue, traces, out var senseDetails);
                    if (!string.IsNullOrWhiteSpace(senseDetails))
                    {
                        details.Add(senseDetails);
                    }

                    overallOutcome = MergeOutcome(overallOutcome, senseOutcome);
                }

                if (row.DelayMs > 0)
                {
                    AdvanceTime(row.DelayMs);
                }

                continue;
            }

            if (!row.Mode.Equals("Acquisition", StringComparison.OrdinalIgnoreCase))
            {
                details.Add($"AM4N unbekannter Modus '{row.Mode}'.");
                continue;
            }

            if (row.DelayMs > 0)
            {
                AdvanceTime(row.DelayMs);
            }

            if (row.AcquisitionDelayMs > 0)
            {
                AdvanceTime(row.AcquisitionDelayMs);
            }

            var readOutcome = TryReadAm4Acquisition(row, out var measured, out var readDetails);
            if (!string.IsNullOrWhiteSpace(readDetails))
            {
                details.Add(readDetails);
            }

            if (row.AcquisitionTimeMs > 0)
            {
                AdvanceTime(row.AcquisitionTimeMs);
            }

            if (!string.IsNullOrWhiteSpace(row.VariableName) && measured.HasValue)
            {
                _context.SetValue(row.VariableName, measured.Value);
            }

            var limitsOutcome = EvaluateAm4Limits(measured, row.EvalMin, row.EvalMax);
            var rangeOutcome = EvaluateRangeOutcome(measured, row.RangeLimit);
            if (rangeOutcome != TestOutcome.Pass)
            {
                details.Add(BuildRangeSummary(row, measured));
            }

            var rowOutcome = MergeOutcome(MergeOutcome(readOutcome, limitsOutcome), rangeOutcome);
            overallOutcome = MergeOutcome(overallOutcome, rowOutcome);

            var unit = row.AcquisitionType.Contains("current", StringComparison.OrdinalIgnoreCase) ? "A" : "V";
            RecordCurvePoint($"AM4 {row.SignalName}", measured, unit);

            if (!string.IsNullOrWhiteSpace(row.SignalName))
            {
                traces.AddRange(CollectSignalTraces(row.SignalName, "AM4 Acquisition"));
            }

            if (!string.IsNullOrWhiteSpace(row.AcquisitionChannel2))
            {
                traces.AddRange(CollectSignalTraces(row.AcquisitionChannel2, "AM4 Acquisition Ref"));
            }

            details.Add(BuildAm4ResultSummary(row, measured, rowOutcome, unit));
        }

        PublishStepEvaluation(
            test,
            overallOutcome,
            details: string.Join(" | ", details.Where(item => !string.IsNullOrWhiteSpace(item))),
            traces: traces);
        return overallOutcome;
    }

    /// <summary>
    /// Executes TryReadAm4Acquisition.
    /// </summary>
    private TestOutcome TryReadAm4Acquisition(Am4Row row, out double? measured, out string? details)
    {
        measured = null;
        details = null;

        if (string.IsNullOrWhiteSpace(row.SignalName))
        {
            details = $"Acquisition [{row.Index}] ohne Signal.";
            return TestOutcome.Error;
        }

        if (!TryReadSignal(row.SignalName, out var rawValue, out var readDetails))
        {
            details = $"Acquisition {row.SignalName}: kein Messwert ({readDetails}).";
            return TestOutcome.Error;
        }

        var primaryValue = _evaluator.ToDouble(rawValue);
        if (primaryValue == null)
        {
            details = $"Acquisition {row.SignalName}: ungueltiger Messwert.";
            return TestOutcome.Error;
        }

        if (!string.IsNullOrWhiteSpace(row.AcquisitionChannel2) &&
            !row.AcquisitionChannel2.Equals("AGND", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadSignal(row.AcquisitionChannel2, out var rawRef, out var refDetails))
            {
                details = $"Acquisition {row.SignalName}: Referenz {row.AcquisitionChannel2} fehlt ({refDetails}).";
                return TestOutcome.Error;
            }

            var refValue = _evaluator.ToDouble(rawRef);
            if (refValue == null)
            {
                details = $"Acquisition {row.SignalName}: Referenz {row.AcquisitionChannel2} ungueltig.";
                return TestOutcome.Error;
            }

            measured = primaryValue.Value - refValue.Value;
        }
        else
        {
            measured = primaryValue.Value;
        }

        return TestOutcome.Pass;
    }

    /// <summary>
    /// Executes BuildAm4ResultSummary.
    /// </summary>
    private static string BuildAm4ResultSummary(Am4Row row, double? measured, TestOutcome outcome, string unit)
    {
        var limits = row.EvalMin.HasValue || row.EvalMax.HasValue
            ? $"{FormatLimit(row.EvalMin)} .. {FormatLimit(row.EvalMax)}"
            : "-";
        var measuredText = measured.HasValue
            ? measured.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";
        var channelSuffix = string.IsNullOrWhiteSpace(row.AcquisitionChannel2)
            ? string.Empty
            : $" (ref {row.AcquisitionChannel2})";
        return $"{row.AcquisitionType} {row.SignalName}{channelSuffix}: {measuredText} {unit} (limits {limits}) => {outcome.ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// Executes EvaluateAm4Limits.
    /// </summary>
    private static TestOutcome EvaluateAm4Limits(double? measured, double? lower, double? upper)
    {
        if (!lower.HasValue && !upper.HasValue)
        {
            return measured.HasValue ? TestOutcome.Pass : TestOutcome.Error;
        }

        return EvaluateNumericOutcome(measured, lower, upper);
    }

    /// <summary>
    /// Executes EvaluateRangeOutcome.
    /// </summary>
    private static TestOutcome EvaluateRangeOutcome(double? measured, double? rangeLimit)
    {
        if (!rangeLimit.HasValue)
        {
            return TestOutcome.Pass;
        }

        if (!measured.HasValue)
        {
            return TestOutcome.Error;
        }

        return Math.Abs(measured.Value) <= rangeLimit.Value ? TestOutcome.Pass : TestOutcome.Error;
    }

    /// <summary>
    /// Executes BuildRangeSummary.
    /// </summary>
    private static string BuildRangeSummary(Am4Row row, double? measured)
    {
        var measuredText = measured.HasValue
            ? measured.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";
        var limitText = row.RangeLimit.HasValue
            ? row.RangeLimit.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "-";
        return $"{row.AcquisitionType} {row.SignalName}: {measuredText} (Range ±{limitText})";
    }

    /// <summary>
    /// Executes EvaluateSenseLimits.
    /// </summary>
    private TestOutcome EvaluateSenseLimits(Am4Row row, double targetValue, List<StepConnectionTrace> traces, out string? details)
    {
        details = null;
        var senseValue = targetValue;

        if (row.SenseMode.Equals("External", StringComparison.OrdinalIgnoreCase))
        {
            var senseSignal = ResolveAm4SenseSignal(row.SignalName);
            if (string.IsNullOrWhiteSpace(senseSignal))
            {
                details = $"Sense: keine SENSE-Leitung fuer {row.SignalName} gefunden.";
                return TestOutcome.Error;
            }

            if (!TryReadSignal(senseSignal, out var senseRaw, out var senseDetails))
            {
                details = $"Sense {senseSignal}: kein Messwert ({senseDetails}).";
                return TestOutcome.Error;
            }

            var numeric = _evaluator.ToDouble(senseRaw);
            if (!numeric.HasValue)
            {
                details = $"Sense {senseSignal}: ungueltiger Messwert.";
                return TestOutcome.Error;
            }

            senseValue = numeric.Value;
            traces.AddRange(CollectSignalTraces(senseSignal, "AM4 Sense"));
        }

        var outcome = EvaluateNumericOutcome(senseValue, row.SenseMin, row.SenseMax);
        details = $"Sense {row.SignalName}: {senseValue.ToString("0.###", CultureInfo.InvariantCulture)} V (limits {FormatLimit(row.SenseMin)} .. {FormatLimit(row.SenseMax)}) => {outcome.ToString().ToUpperInvariant()}";
        return outcome;
    }

    /// <summary>
    /// Executes ResolveAm4SenseSignal.
    /// </summary>
    private static string? ResolveAm4SenseSignal(string? stimulusSignal)
    {
        if (string.IsNullOrWhiteSpace(stimulusSignal))
        {
            return null;
        }

        var trimmed = stimulusSignal.Trim();
        if (trimmed.StartsWith("AM4_STI", StringComparison.OrdinalIgnoreCase))
        {
            return $"AM4_SENSE{trimmed.Substring("AM4_STI".Length)}";
        }

        return null;
    }

    /// <summary>
    /// Executes EnumerateAm4Rows.
    /// </summary>
    private IEnumerable<Am4Row> EnumerateAm4Rows(TestParameters parameters)
    {
        foreach (var record in parameters.Records)
        {
            if (IsDisabled(record.Disabled))
            {
                continue;
            }

            var mode = NormalizeQuotedText(GetRecordAttribute(record, "RowMode")) ?? "Acquisition";
            var signalName = NormalizeAm4Signal(ExtractSignalName(GetRecordAttribute(record, "SignalName")));
            var rowComment = NormalizeQuotedText(GetRecordAttribute(record, "RowComment"));
            var index = int.TryParse(record.Index, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex : 0;

            var stState = NormalizeQuotedText(GetRecordAttribute(record, "stState")) ?? "Off";
            var stShunt = NormalizeQuotedText(GetRecordAttribute(record, "stShunt"));
            var stSense = NormalizeQuotedText(GetRecordAttribute(record, "stSense")) ?? "Internal";
            var stSenseMin = ParseEngineeringValue(GetRecordAttribute(record, "stSenseMin"));
            var stSenseMax = ParseEngineeringValue(GetRecordAttribute(record, "stSenseMax"));
            var stUout = ParseEngineeringValue(GetRecordAttribute(record, "stUout"));
            var stRamp = NormalizeQuotedText(GetRecordAttribute(record, "stRamp")) ?? "Off";
            var stRampTime = ParseDurationMilliseconds(GetRecordAttribute(record, "stRampTime"));

            var acqType = NormalizeQuotedText(GetRecordAttribute(record, "acqType")) ?? "Input voltage";
            var acqRange = NormalizeQuotedText(GetRecordAttribute(record, "acqRange"));
            var acqTime = ParseDurationMilliseconds(GetRecordAttribute(record, "acqTime"));
            var acqDelay = ParseDurationMilliseconds(GetRecordAttribute(record, "acqDelay"));
            var acqChan2 = NormalizeAm4Signal(ExtractSignalName(GetRecordAttribute(record, "acqChan2")));
            var acqEvalMin = ParseEngineeringValue(GetRecordAttribute(record, "acqEvalMin"));
            var acqEvalMax = ParseEngineeringValue(GetRecordAttribute(record, "acqEvalMax"));
            var acqVariable = NormalizeQuotedText(GetRecordAttribute(record, "acqVariable"));
            var delTime = ParseDurationMilliseconds(GetRecordAttribute(record, "delTime"));
            var rangeLimit = ParseAcqRangeLimit(acqRange);

            yield return new Am4Row(
                index,
                mode,
                signalName,
                rowComment,
                stState.Equals("On", StringComparison.OrdinalIgnoreCase),
                stShunt,
                stSense,
                stSenseMin,
                stSenseMax,
                stUout,
                stRamp.Equals("On", StringComparison.OrdinalIgnoreCase),
                stRampTime,
                acqType,
                acqRange,
                rangeLimit,
                acqTime,
                acqDelay,
                acqChan2,
                acqEvalMin,
                acqEvalMax,
                acqVariable,
                delTime);
        }
    }

    /// <summary>
    /// Executes MergeOutcome.
    /// </summary>
    private static TestOutcome MergeOutcome(TestOutcome current, TestOutcome next)
    {
        if (current == TestOutcome.Error || next == TestOutcome.Error)
        {
            return TestOutcome.Error;
        }

        if (current == TestOutcome.Fail || next == TestOutcome.Fail)
        {
            return TestOutcome.Fail;
        }

        return TestOutcome.Pass;
    }

    private static string? NormalizeAm4Signal(string? signalName)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            return null;
        }

        var trimmed = signalName.Trim();
        if (trimmed.Replace(" ", string.Empty).Equals("<AGND>", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("AGND", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static double? ParseAcqRangeLimit(string? rawRange)
    {
        if (string.IsNullOrWhiteSpace(rawRange))
        {
            return null;
        }

        var normalized = rawRange.Trim().Trim('\'', '"').Replace("±", string.Empty);
        if (normalized.Equals("Coarse", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parsed = ParseEngineeringValue(normalized);
        return parsed.HasValue ? Math.Abs(parsed.Value) : null;
    }

    private sealed record Am4Row(
        int Index,
        string Mode,
        string? SignalName,
        string? Comment,
        bool StateOn,
        string? Shunt,
        string SenseMode,
        double? SenseMin,
        double? SenseMax,
        double? OutputVoltage,
        bool RampOn,
        long RampTimeMs,
        string AcquisitionType,
        string? AcquisitionRange,
        double? RangeLimit,
        long AcquisitionTimeMs,
        long AcquisitionDelayMs,
        string? AcquisitionChannel2,
        double? EvalMin,
        double? EvalMax,
        string? VariableName,
        long DelayMs);
}
