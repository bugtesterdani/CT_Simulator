using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx CTCT contact tests against the active DUT-side wiring and simulation state.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    /// <summary>
    /// Executes RunConnectionContactTest.
    /// </summary>
    private TestOutcome RunConnectionContactTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "CTCT ohne Parameter.");
            return TestOutcome.Error;
        }

        if (_wireVizResolver == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "CTCT ohne geladene Verdrahtung.");
            return TestOutcome.Error;
        }

        var records = parameters.Tables
            .SelectMany(table => table.Records)
            .Where(record =>
                !string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(record.TestPoint))
            .ToList();

        if (records.Count < 2)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "CTCT ohne ausreichende TPT-Datensaetze.");
            return TestOutcome.Error;
        }

        var overall = TestOutcome.Pass;
        foreach (var record in records)
        {
            var evaluation = EvaluateCtctRecord(record, records);
            RecordCurvePoint(
                $"CTCT {record.TestPoint}",
                evaluation.MeasuredResistanceOhms.HasValue
                    ? Math.Min(evaluation.MeasuredResistanceOhms.Value, 10_000_000d)
                    : null,
                "Ohm");
            PublishStepEvaluation(
                test,
                evaluation.Outcome,
                measured: evaluation.MeasuredResistanceOhms,
                lower: evaluation.MinimumResistanceOhms,
                upper: evaluation.MaximumResistanceOhms,
                unit: "Ohm",
                details: evaluation.Details,
                traces: evaluation.Traces,
                curvePoints: CaptureCurvePoints(),
                stepNameOverride: record.TestPoint);
            overall = CombineOutcomes(overall, evaluation.Outcome);
        }

        return overall;
    }

    /// <summary>
    /// Executes EvaluateCtctRecord.
    /// </summary>
    private CtctRecordEvaluation EvaluateCtctRecord(Record sourceRecord, IReadOnlyList<Record> allRecords)
    {
        var sourcePoint = sourceRecord.TestPoint?.Trim() ?? string.Empty;
        var thresholdOhms = ParseEngineeringValue(sourceRecord.Resistance);
        var expectedClosed = string.Equals(sourceRecord.SwitchState?.Trim(), "closed", StringComparison.OrdinalIgnoreCase);
        var minimumResistanceOhms = expectedClosed ? null : thresholdOhms;
        var maximumResistanceOhms = expectedClosed ? thresholdOhms : null;

        var pairMeasurements = new List<WireVizResistanceMeasurement>();
        foreach (var targetRecord in allRecords)
        {
            var targetPoint = targetRecord.TestPoint?.Trim();
            if (string.IsNullOrWhiteSpace(targetPoint) ||
                string.Equals(sourcePoint, targetPoint, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pairMeasurements.Add(_wireVizResolver!.MeasureResistance(
                sourcePoint,
                targetPoint,
                _signalState,
                _signalChangedAtMs,
                _simulatedTimeMs,
                _faults));
        }

        var unresolved = pairMeasurements.FirstOrDefault(item => !item.SourceResolved || !item.TargetResolved);
        if (unresolved != null)
        {
            return new CtctRecordEvaluation(
                TestOutcome.Error,
                null,
                minimumResistanceOhms,
                maximumResistanceOhms,
                $"{sourcePoint}: {unresolved.FailureReason ?? "Testpunkt konnte nicht aufgeloest werden."}",
                Array.Empty<StepConnectionTrace>());
        }

        var connectedMeasurements = pairMeasurements
            .Where(item => item.PathFound && item.ResistanceOhms.HasValue)
            .OrderBy(item => item.ResistanceOhms!.Value)
            .ToList();

        var bestMeasurement = connectedMeasurements.FirstOrDefault();
        var bestMeasurements = SelectBestMeasurements(connectedMeasurements);
        if (expectedClosed)
        {
            if (bestMeasurement == null)
            {
                return new CtctRecordEvaluation(
                    TestOutcome.Fail,
                    null,
                    null,
                    maximumResistanceOhms,
                    $"{sourcePoint}: offene Leitung, kein aktiver Pfad zu einem der anderen CTCT-Testpunkte.",
                    Array.Empty<StepConnectionTrace>());
            }

            var pass = !thresholdOhms.HasValue || bestMeasurement.ResistanceOhms!.Value <= thresholdOhms.Value;
            return new CtctRecordEvaluation(
                pass ? TestOutcome.Pass : TestOutcome.Fail,
                bestMeasurement.ResistanceOhms,
                null,
                maximumResistanceOhms,
                BuildCtctDetailText(sourcePoint, expectedClosed, thresholdOhms, pairMeasurements, bestMeasurement),
                BuildCtctTraces(sourcePoint, bestMeasurements));
        }

        if (bestMeasurement == null)
        {
            return new CtctRecordEvaluation(
                TestOutcome.Pass,
                null,
                minimumResistanceOhms,
                null,
                BuildCtctDetailText(sourcePoint, expectedClosed, thresholdOhms, pairMeasurements, null),
                Array.Empty<StepConnectionTrace>());
        }

        var isolated = !thresholdOhms.HasValue || bestMeasurement.ResistanceOhms!.Value >= thresholdOhms.Value;
        return new CtctRecordEvaluation(
            isolated ? TestOutcome.Pass : TestOutcome.Fail,
            bestMeasurement.ResistanceOhms,
            minimumResistanceOhms,
            null,
            BuildCtctDetailText(sourcePoint, expectedClosed, thresholdOhms, pairMeasurements, bestMeasurement),
            BuildCtctTraces(sourcePoint, bestMeasurements));
    }

    /// <summary>
    /// Executes BuildCtctDetailText.
    /// </summary>
    private static string BuildCtctDetailText(
        string sourcePoint,
        bool expectedClosed,
        double? thresholdOhms,
        IReadOnlyList<WireVizResistanceMeasurement> pairMeasurements,
        WireVizResistanceMeasurement? bestMeasurement)
    {
        var expectation = expectedClosed
            ? $"closed <= {FormatEngineeringValue(thresholdOhms)}"
            : $"open >= {FormatEngineeringValue(thresholdOhms)}";
        var pairTexts = pairMeasurements
            .Select(item =>
            {
                if (!item.PathFound)
                {
                    return $"{sourcePoint}->{item.TargetSignalName}: open";
                }

                var elementText = item.EdgeDescriptions.Count == 0
                    ? string.Empty
                    : $" via {string.Join(", ", item.EdgeDescriptions)}";
                return $"{sourcePoint}->{item.TargetSignalName}: {FormatEngineeringValue(item.ResistanceOhms)}{elementText}";
            })
            .ToList();

        var bestText = bestMeasurement == null
            ? "beste Verbindung: keine"
            : $"beste Verbindung: {bestMeasurement.TargetSignalName} = {FormatEngineeringValue(bestMeasurement.ResistanceOhms)}";
        return $"{sourcePoint}: erwartet {expectation}, {bestText}, Messungen=[{string.Join("; ", pairTexts)}]";
    }

    /// <summary>
    /// Executes BuildCtctTraces.
    /// </summary>
    private static IReadOnlyList<StepConnectionTrace> BuildCtctTraces(string sourcePoint, IReadOnlyList<WireVizResistanceMeasurement> measurements)
    {
        if (measurements.Count == 0)
        {
            return Array.Empty<StepConnectionTrace>();
        }

        var traces = new List<StepConnectionTrace>();
        foreach (var measurement in measurements)
        {
            if (!measurement.PathFound || measurement.Nodes.Count == 0)
            {
                continue;
            }

            var title = $"CTCT: {sourcePoint} -> {measurement.TargetSignalName} ({FormatEngineeringValue(measurement.ResistanceOhms)})";
            traces.Add(new StepConnectionTrace(title, measurement.Nodes));
        }

        return traces;
    }

    /// <summary>
    /// Executes SelectBestMeasurements.
    /// </summary>
    private static IReadOnlyList<WireVizResistanceMeasurement> SelectBestMeasurements(IReadOnlyList<WireVizResistanceMeasurement> measurements)
    {
        if (measurements.Count == 0)
        {
            return Array.Empty<WireVizResistanceMeasurement>();
        }

        var minValue = measurements.Min(item => item.ResistanceOhms ?? double.MaxValue);
        const double epsilon = 1e-6;
        return measurements
            .Where(item => item.ResistanceOhms.HasValue && Math.Abs(item.ResistanceOhms.Value - minValue) <= epsilon)
            .ToList();
    }

    /// <summary>
    /// Executes FormatEngineeringValue.
    /// </summary>
    private static string FormatEngineeringValue(double? value)
    {
        if (!value.HasValue)
        {
            return "n/a";
        }

        if (value.Value >= 1_000_000d)
        {
            return $"{(value.Value / 1_000_000d).ToString("0.###", CultureInfo.InvariantCulture)} MOhm";
        }

        if (value.Value >= 1_000d)
        {
            return $"{(value.Value / 1_000d).ToString("0.###", CultureInfo.InvariantCulture)} kOhm";
        }

        return $"{value.Value.ToString("0.###", CultureInfo.InvariantCulture)} Ohm";
    }

    /// <summary>
    /// Executes CtctRecordEvaluation.
    /// </summary>
    private sealed record CtctRecordEvaluation(
        TestOutcome Outcome,
        double? MeasuredResistanceOhms,
        double? MinimumResistanceOhms,
        double? MaximumResistanceOhms,
        string Details,
        IReadOnlyList<StepConnectionTrace> Traces);
}
