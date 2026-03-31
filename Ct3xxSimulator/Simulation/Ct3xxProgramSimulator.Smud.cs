using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Executes CT3xx SMUD power-supply steps against the shared signal and device runtime.
/// </summary>
public partial class Ct3xxProgramSimulator
{
    private readonly HashSet<string> _blownSmudSupplySignals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes RunSmudPowerSupplyTest.
    /// </summary>
    private TestOutcome RunSmudPowerSupplyTest(Test test)
    {
        var parameters = test.Parameters;
        if (parameters == null)
        {
            PublishStepEvaluation(test, TestOutcome.Error, details: "SMUD ohne Parameter.");
            return TestOutcome.Error;
        }

        var runtime = BuildSmudRuntime(parameters);
        if (_blownSmudSupplySignals.Contains(runtime.SupplySignal))
        {
            WriteSignal(runtime.SupplySignal, 0d);
            RecordCurvePoint($"{runtime.ModuleName} Versorgung", 0d, "V");
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                measured: 0d,
                lower: runtime.MinimumCurrent,
                upper: runtime.MaximumCurrent,
                unit: "A",
                details: $"{runtime.ModuleName}: Sicherung bereits ausgeloest.",
                traces: BuildSmudTraces(runtime),
                curvePoints: CaptureCurvePoints());
            return TestOutcome.Error;
        }

        var requestedVoltage = runtime.SupplyEnabled ? runtime.Voltage : 0d;
        WriteSignal(runtime.SupplySignal, requestedVoltage);
        WriteSignal(runtime.GroundSignal, 0d);
        RecordCurvePoint($"{runtime.ModuleName} Versorgung", requestedVoltage, "V");

        if (runtime.MeasurementDelayMs > 0)
        {
            AdvanceTime(runtime.MeasurementDelayMs);
        }

        var traces = BuildSmudTraces(runtime);
        var details = new List<string>
        {
            $"{runtime.ModuleName}: Versorgung {(runtime.SupplyEnabled ? "ein" : "aus")}",
            $"U={runtime.Voltage.ToString("0.###", CultureInfo.InvariantCulture)} V",
            $"Fuse={runtime.FuseCurrent?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-"} A",
            $"CurrentMeasurement={(runtime.CurrentMeasurementEnabled ? "On" : "Off")}",
            $"Delay={runtime.MeasurementDelayMs} ms"
        };

        if (!runtime.CurrentMeasurementEnabled)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Pass,
                measured: requestedVoltage,
                lower: runtime.SupplyEnabled ? runtime.Voltage : 0d,
                upper: runtime.SupplyEnabled ? runtime.Voltage : 0d,
                unit: "V",
                details: string.Join(", ", details),
                traces: traces,
                curvePoints: CaptureCurvePoints());
            return TestOutcome.Pass;
        }

        if (_externalDeviceSession == null)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                lower: runtime.MinimumCurrent,
                upper: runtime.MaximumCurrent,
                unit: "A",
                details: "SMUD-Strommessung ohne aktive Geraetesimulation ist nicht moeglich.",
                traces: traces,
                curvePoints: CaptureCurvePoints());
            return TestOutcome.Error;
        }

        if (!TryReadSignal(runtime.CurrentSignal, out var rawCurrent, out var measurementSource) ||
            _evaluator.ToDouble(rawCurrent) is not double measuredCurrent)
        {
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                lower: runtime.MinimumCurrent,
                upper: runtime.MaximumCurrent,
                unit: "A",
                details: $"{runtime.ModuleName}: Strommessung fehlgeschlagen{(string.IsNullOrWhiteSpace(measurementSource) ? string.Empty : $" ({measurementSource})")}.",
                traces: traces,
                curvePoints: CaptureCurvePoints());
            return TestOutcome.Error;
        }

        RecordCurvePoint($"{runtime.ModuleName} Strom", measuredCurrent, "A");
        details.Add($"I={measuredCurrent.ToString("0.###", CultureInfo.InvariantCulture)} A");
        if (!string.IsNullOrWhiteSpace(measurementSource))
        {
            details.Add(measurementSource!);
        }

        if (runtime.FuseCurrent.HasValue && measuredCurrent > runtime.FuseCurrent.Value)
        {
            _blownSmudSupplySignals.Add(runtime.SupplySignal);
            WriteSignal(runtime.SupplySignal, 0d);
            RecordCurvePoint($"{runtime.ModuleName} Versorgung", 0d, "V");
            details.Add($"Sicherung ausgeloest bei {measuredCurrent.ToString("0.###", CultureInfo.InvariantCulture)} A > {runtime.FuseCurrent.Value.ToString("0.###", CultureInfo.InvariantCulture)} A");
            PublishStepEvaluation(
                test,
                TestOutcome.Error,
                measured: measuredCurrent,
                lower: runtime.MinimumCurrent,
                upper: runtime.MaximumCurrent,
                unit: "A",
                details: string.Join(", ", details),
                traces: traces,
                curvePoints: CaptureCurvePoints());
            return TestOutcome.Error;
        }

        var outcome = EvaluateNumericOutcome(measuredCurrent, runtime.MinimumCurrent, runtime.MaximumCurrent);
        details.Add($"Grenzen=[{FormatLimit(runtime.MinimumCurrent)}..{FormatLimit(runtime.MaximumCurrent)}] A");
        PublishStepEvaluation(
            test,
            outcome,
            measured: measuredCurrent,
            lower: runtime.MinimumCurrent,
            upper: runtime.MaximumCurrent,
            unit: "A",
            details: string.Join(", ", details),
            traces: traces,
            curvePoints: CaptureCurvePoints());
        return outcome;
    }

    /// <summary>
    /// Executes BuildSmudRuntime.
    /// </summary>
    private SmudRuntime BuildSmudRuntime(TestParameters parameters)
    {
        var moduleName = ResolveSmudModuleName(parameters);
        var supplyEnabled = (NormalizeQuotedText(GetParameterAttribute(parameters, "SupplyOnOff")) ?? "off")
            .Equals("on", StringComparison.OrdinalIgnoreCase);
        var voltage = ParseEngineeringValue(GetParameterAttribute(parameters, "Voltage")) ?? 0d;
        var fuseCurrent = ParseEngineeringValue(GetParameterAttribute(parameters, "Fuse"));
        var minimumCurrent = ParseEngineeringValue(GetParameterAttribute(parameters, "MinPermissibleCurrent"));
        var maximumCurrent = ParseEngineeringValue(GetParameterAttribute(parameters, "MaxPermissibleCurrent"));
        var measurementDelayMs = ParseDurationMilliseconds(GetParameterAttribute(parameters, "MeasurementDelay"));
        var currentMeasurementEnabled = ParseYesNo(GetParameterAttribute(parameters, "CurrentMeasurement"), defaultValue: false);

        return new SmudRuntime(
            moduleName,
            $"{moduleName}_VOUT",
            $"{moduleName}_ISENSE",
            $"{moduleName}_GND",
            supplyEnabled,
            voltage,
            fuseCurrent,
            minimumCurrent,
            maximumCurrent,
            currentMeasurementEnabled,
            measurementDelayMs);
    }

    /// <summary>
    /// Executes BuildSmudTraces.
    /// </summary>
    private IReadOnlyList<StepConnectionTrace> BuildSmudTraces(SmudRuntime runtime)
    {
        var traces = new List<StepConnectionTrace>();
        traces.AddRange(CollectSignalTraces(runtime.SupplySignal, "SMUD Versorgung"));
        traces.AddRange(CollectSignalTraces(runtime.CurrentSignal, "SMUD Strommessung"));
        traces.AddRange(CollectSignalTraces(runtime.GroundSignal, "SMUD Masse"));

        return traces
            .GroupBy(trace => $"{trace.Title}|{string.Join(">", trace.Nodes)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Executes ResolveSmudModuleName.
    /// </summary>
    private static string ResolveSmudModuleName(TestParameters parameters)
    {
        var candidates = new[]
        {
            parameters.Name,
            parameters.DrawingReference,
            GetParameterAttribute(parameters, "Module")
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = Regex.Match(candidate, @"\b([A-Z]{2,}\d+)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }
        }

        return "SM2";
    }

    /// <summary>
    /// Executes SmudRuntime.
    /// </summary>
    private sealed record SmudRuntime(
        string ModuleName,
        string SupplySignal,
        string CurrentSignal,
        string GroundSignal,
        bool SupplyEnabled,
        double Voltage,
        double? FuseCurrent,
        double? MinimumCurrent,
        double? MaximumCurrent,
        bool CurrentMeasurementEnabled,
        long MeasurementDelayMs);
}
