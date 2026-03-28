// Provides Wire Viz Harness Resolver Graph Elements for the module runtime simulation support.
using System;
using System.Collections.Generic;
using System.Globalization;
using Ct3xxSimulationModelParser.Model;
using Ct3xxSimulator.Simulation.FaultInjection;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed partial class WireVizHarnessResolver
{
    private sealed partial class WireVizGraph
    {
        private IEnumerable<TraversalEdge> GetDynamicTargets(
            string current,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults,
            bool forWrite)
        {
            foreach (var element in _simulationElements)
            {
                switch (element)
                {
                    case ResistorElementDefinition resistor:
                        foreach (var edge in YieldResistiveConnection(current, resistor.A, resistor.B, faults.ResolveResistanceValue(resistor.Id, resistor.Ohms), currentTimeMs, faults, resistor.Id))
                        {
                            yield return edge;
                        }
                        break;
                    case RelayElementDefinition relay when IsRelayClosed(relay, signalState, signalTimes, currentTimeMs, faults):
                        foreach (var contact in relay.Contacts)
                        {
                            foreach (var edge in YieldBidirectional(current, contact.A, contact.B, currentTimeMs, faults))
                            {
                                yield return edge;
                            }
                        }
                        break;
                    case TransformerElementDefinition transformer:
                        foreach (var edge in YieldTransformerCoupling(current, transformer.PrimaryA, transformer.SecondaryA, transformer.Ratio, currentTimeMs, faults, transformer.Id))
                        {
                            yield return edge;
                        }

                        foreach (var edge in YieldTransformerCoupling(current, transformer.PrimaryB, transformer.SecondaryB, transformer.Ratio, currentTimeMs, faults, transformer.Id))
                        {
                            yield return edge;
                        }
                        break;
                    case CurrentTransformerElementDefinition currentTransformer:
                        foreach (var edge in YieldCurrentTransformerTargets(current, currentTransformer, faults, forWrite))
                        {
                            yield return edge;
                        }
                        break;
                    case UnknownElementDefinition unknown:
                        foreach (var edge in GetUnknownElementTargets(current, unknown, signalState, signalTimes, currentTimeMs, faults))
                        {
                            yield return edge;
                        }
                        break;
                }
            }
        }

        private IEnumerable<TraversalEdge> GetUnknownElementTargets(
            string current,
            UnknownElementDefinition unknown,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults)
        {
            switch (unknown.Type.ToLowerInvariant())
            {
                case "switch":
                    if (IsGenericSwitchClosed(unknown, signalState, signalTimes, currentTimeMs))
                    {
                        foreach (var edge in YieldBidirectional(current, ReadMetadata(unknown, "a"), ReadMetadata(unknown, "b"), currentTimeMs, faults))
                        {
                            yield return edge;
                        }
                    }
                    break;
                case "fuse":
                    if (!faults.IsBlownFuse(unknown.Id))
                    {
                        foreach (var edge in YieldBidirectional(current, ReadMetadata(unknown, "a"), ReadMetadata(unknown, "b"), currentTimeMs, faults))
                        {
                            yield return edge;
                        }
                    }
                    break;
                case "load":
                    var loadOhms = double.TryParse(ReadMetadata(unknown, "ohms"), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLoad)
                        ? parsedLoad
                        : 1d;
                    foreach (var edge in YieldResistiveConnection(current, ReadMetadata(unknown, "a"), ReadMetadata(unknown, "b"), faults.ResolveResistanceValue(unknown.Id, loadOhms), currentTimeMs, faults, unknown.Id))
                    {
                        yield return edge;
                    }
                    break;
                case "voltage_divider":
                    foreach (var edge in YieldVoltageDividerTargets(current, unknown, currentTimeMs, faults))
                    {
                        yield return edge;
                    }
                    break;
                case "diode":
                    foreach (var edge in YieldDirected(current, ReadMetadata(unknown, "anode"), ReadMetadata(unknown, "cathode"), currentTimeMs, faults))
                    {
                        yield return edge;
                    }
                    break;
                case "sensor":
                    foreach (var edge in YieldSensorTargets(current, unknown, signalState))
                    {
                        yield return edge;
                    }
                    break;
                case "opto":
                    if (IsOptoActive(unknown, signalState, signalTimes, currentTimeMs))
                    {
                        foreach (var edge in YieldBidirectional(current, ReadMetadata(unknown, "output_a"), ReadMetadata(unknown, "output_b"), currentTimeMs, faults))
                        {
                            yield return edge;
                        }
                    }
                    break;
                case "transistor":
                    if (IsTransistorActive(unknown, signalState, signalTimes, currentTimeMs))
                    {
                        var collector = ReadMetadata(unknown, "collector");
                        var emitter = ReadMetadata(unknown, "emitter");
                        var drain = ReadMetadata(unknown, "drain");
                        var source = ReadMetadata(unknown, "source");
                        var transistorType = ReadMetadata(unknown, "transistor_type");
                        if (string.IsNullOrWhiteSpace(transistorType))
                        {
                            transistorType = ReadMetadata(unknown, "device_type");
                        }

                        foreach (var edge in YieldTransistorConnection(
                                     current,
                                     string.IsNullOrWhiteSpace(collector) ? drain : collector,
                                     string.IsNullOrWhiteSpace(emitter) ? source : emitter,
                                     transistorType,
                                     currentTimeMs,
                                     faults))
                        {
                            yield return edge;
                        }
                    }
                    break;
            }
        }

        private static string ReadMetadata(UnknownElementDefinition definition, string key)
        {
            return definition.Metadata.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        }

        internal static bool IsGenericSwitchClosed(
            UnknownElementDefinition definition,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs)
        {
            var controlSignal = ReadMetadata(definition, "control_signal");
            if (string.IsNullOrWhiteSpace(controlSignal))
            {
                return true;
            }

            var thresholdText = ReadMetadata(definition, "threshold_v");
            var threshold = double.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThreshold)
                ? parsedThreshold
                : 0.5d;
            var normallyClosed = string.Equals(ReadMetadata(definition, "normally_closed"), "true", StringComparison.OrdinalIgnoreCase);
            var numeric = signalState != null && signalState.TryGetValue(controlSignal, out var control)
                ? ParseSignalValue(control)
                : 0d;
            var active = numeric >= threshold;
            var desired = normallyClosed ? !active : active;

            if (signalTimes == null || !signalTimes.TryGetValue(controlSignal, out var changedAt))
            {
                return desired;
            }

            var debounceMs = TryReadLongMetadata(definition, "debounce_ms");
            if (debounceMs > 0 && currentTimeMs - changedAt < debounceMs)
            {
                return !desired;
            }

            var delayKey = desired ? "close_delay_ms" : "open_delay_ms";
            var switchDelayMs = TryReadLongMetadata(definition, delayKey);
            if (switchDelayMs > 0 && currentTimeMs - changedAt < switchDelayMs)
            {
                return !desired;
            }

            return desired;
        }

        private static bool IsOptoActive(
            UnknownElementDefinition definition,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs)
        {
            var ledSignal = ReadMetadata(definition, "led_signal");
            if (!string.IsNullOrWhiteSpace(ledSignal))
            {
                return IsControlledBySignal(definition, ledSignal, signalState, signalTimes, currentTimeMs);
            }

            return IsGenericSwitchClosed(definition, signalState, signalTimes, currentTimeMs);
        }

        private static bool IsTransistorActive(
            UnknownElementDefinition definition,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs)
        {
            var gateSignal = ReadMetadata(definition, "gate_signal");
            if (string.IsNullOrWhiteSpace(gateSignal))
            {
                gateSignal = ReadMetadata(definition, "base_signal");
            }

            if (!string.IsNullOrWhiteSpace(gateSignal))
            {
                return IsControlledBySignal(definition, gateSignal, signalState, signalTimes, currentTimeMs);
            }

            return IsGenericSwitchClosed(definition, signalState, signalTimes, currentTimeMs);
        }

        private static bool IsControlledBySignal(
            UnknownElementDefinition definition,
            string controlSignal,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs)
        {
            var thresholdText = ReadMetadata(definition, "threshold_v");
            var threshold = double.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThreshold)
                ? parsedThreshold
                : 0.5d;
            var normallyClosed = string.Equals(ReadMetadata(definition, "normally_closed"), "true", StringComparison.OrdinalIgnoreCase);
            var numeric = signalState != null && signalState.TryGetValue(controlSignal, out var control)
                ? ParseSignalValue(control)
                : 0d;
            var active = numeric >= threshold;
            var desired = normallyClosed ? !active : active;

            if (signalTimes == null || !signalTimes.TryGetValue(controlSignal, out var changedAt))
            {
                return desired;
            }

            var delayKey = desired ? "close_delay_ms" : "open_delay_ms";
            var switchDelayMs = TryReadLongMetadata(definition, delayKey);
            if (switchDelayMs > 0 && currentTimeMs - changedAt < switchDelayMs)
            {
                return !desired;
            }

            return desired;
        }

        private static long TryReadLongMetadata(UnknownElementDefinition definition, string key)
        {
            return definition.Metadata.TryGetValue(key, out var text) &&
                   long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Max(0L, parsed)
                : 0L;
        }

        private static IEnumerable<TraversalEdge> YieldBidirectional(string current, string a, string b, long currentTimeMs, SimulationFaultSet faults)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || faults.IsOpenConnection(a, b) || faults.HasContactProblem(a, b, currentTimeMs))
            {
                yield break;
            }

            if (string.Equals(current, a, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(b);
            }
            else if (string.Equals(current, b, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(a);
            }
        }

        private static IEnumerable<TraversalEdge> YieldResistiveConnection(string current, string a, string b, double resistanceOhms, long currentTimeMs, SimulationFaultSet faults, string elementId)
        {
            if (resistanceOhms >= 1_000_000_000d)
            {
                yield break;
            }

            foreach (var edge in YieldBidirectional(current, a, b, currentTimeMs, faults))
            {
                yield return TraversalEdge.Node(edge.Target, edge.Scale, $"{elementId}: {resistanceOhms.ToString("0.###", CultureInfo.InvariantCulture)} Ohm", resistanceOhms);
            }
        }

        private static IEnumerable<TraversalEdge> YieldDirected(string current, string from, string to, long currentTimeMs, SimulationFaultSet faults)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || faults.IsOpenConnection(from, to) || faults.HasContactProblem(from, to, currentTimeMs))
            {
                yield break;
            }

            if (string.Equals(current, from, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(to);
            }
        }

        private static IEnumerable<TraversalEdge> YieldTransformerCoupling(string current, string primary, string secondary, double ratio, long currentTimeMs, SimulationFaultSet faults, string elementId)
        {
            if (string.IsNullOrWhiteSpace(primary) || string.IsNullOrWhiteSpace(secondary) || faults.IsOpenConnection(primary, secondary) || faults.HasContactProblem(primary, secondary, currentTimeMs))
            {
                yield break;
            }

            var safeRatio = Math.Abs(ratio) < double.Epsilon ? 1d : ratio;
            if (string.Equals(current, primary, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(
                    secondary,
                    1d / safeRatio,
                    $"{elementId}: primary->secondary /{safeRatio.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
            else if (string.Equals(current, secondary, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(
                    primary,
                    safeRatio,
                    $"{elementId}: secondary->primary x{safeRatio.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }

        private static IEnumerable<TraversalEdge> YieldCurrentTransformerTargets(string current, CurrentTransformerElementDefinition transformer, SimulationFaultSet faults, bool forWrite)
        {
            if (forWrite)
            {
                yield break;
            }

            var signalName = transformer.PrimarySignal?.Trim();
            if (string.IsNullOrWhiteSpace(signalName))
            {
                yield break;
            }

            var safeRatio = Math.Abs(transformer.Ratio) < double.Epsilon ? 1d : transformer.Ratio;
            if (string.Equals(current, transformer.SecondaryA, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(current, transformer.SecondaryB, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.SyntheticSignal(
                    signalName,
                    safeRatio,
                    $"{transformer.Id}: secondary current from {signalName} /{safeRatio.ToString("0.###", CultureInfo.InvariantCulture)}",
                    readOnly: true);
            }
        }

        private static IEnumerable<TraversalEdge> YieldVoltageDividerTargets(string current, UnknownElementDefinition definition, long currentTimeMs, SimulationFaultSet faults)
        {
            var input = ReadMetadata(definition, "input");
            var output = ReadMetadata(definition, "output");
            if (string.IsNullOrWhiteSpace(input))
            {
                input = ReadMetadata(definition, "a");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                output = ReadMetadata(definition, "b");
            }

            var ratioText = faults.TryGetResistanceOverride(definition.Id)?.ToString(CultureInfo.InvariantCulture) ?? ReadMetadata(definition, "ratio");
            var ratio = double.TryParse(ratioText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedRatio)
                ? parsedRatio
                : 1d;
            var safeRatio = Math.Abs(ratio) < double.Epsilon ? 1d : ratio;

            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || faults.IsOpenConnection(input, output) || faults.HasContactProblem(input, output, currentTimeMs))
            {
                yield break;
            }

            if (string.Equals(current, input, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(output, safeRatio, $"{definition.Id}: divider x{safeRatio.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
            else if (string.Equals(current, output, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.Node(input, safeRatio == 0d ? 1d : 1d / safeRatio, $"{definition.Id}: divider back {(1d / safeRatio).ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }

        private static IEnumerable<TraversalEdge> YieldSensorTargets(string current, UnknownElementDefinition definition, IReadOnlyDictionary<string, object?>? signalState)
        {
            var inputSignal = ReadMetadata(definition, "input_signal");
            var outputSignal = ReadMetadata(definition, "output_signal");
            var inputNode = ReadMetadata(definition, "input");
            var outputNode = ReadMetadata(definition, "output");
            if (string.IsNullOrWhiteSpace(inputSignal) || string.IsNullOrWhiteSpace(outputSignal))
            {
                if (string.IsNullOrWhiteSpace(outputSignal))
                {
                    yield break;
                }
            }

            var scaleText = ReadMetadata(definition, "scale");
            var scale = double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale)
                ? parsedScale
                : 1d;

            if (!string.IsNullOrWhiteSpace(outputNode) &&
                string.Equals(current, outputNode, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.SyntheticSignal(outputSignal, 1d, $"{definition.Id}: sensor output {outputSignal}", readOnly: true);
            }

            if (!string.IsNullOrWhiteSpace(inputNode) &&
                string.Equals(current, inputNode, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(outputSignal))
            {
                yield return TraversalEdge.SyntheticSignal(outputSignal, scale, $"{definition.Id}: sensor node->{outputSignal} x{scale.ToString("0.###", CultureInfo.InvariantCulture)}", readOnly: true);
            }

            if (!string.IsNullOrWhiteSpace(inputSignal) &&
                current.StartsWith("@signal:", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(current["@signal:".Length..], inputSignal, StringComparison.OrdinalIgnoreCase))
            {
                yield return TraversalEdge.SyntheticSignal(outputSignal, scale, $"{definition.Id}: sensor x{scale.ToString("0.###", CultureInfo.InvariantCulture)}", readOnly: true);
            }
        }

        private static IEnumerable<TraversalEdge> YieldTransistorConnection(string current, string input, string output, string type, long currentTimeMs, SimulationFaultSet faults)
        {
            var normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();
            var bidirectional = normalizedType is "bjt" or "mosfet" or "";
            if (bidirectional)
            {
                foreach (var edge in YieldBidirectional(current, input, output, currentTimeMs, faults))
                {
                    yield return edge;
                }

                yield break;
            }

            var forwardFrom = normalizedType is "npn" or "nmos" ? input : output;
            var forwardTo = normalizedType is "npn" or "nmos" ? output : input;
            foreach (var edge in YieldDirected(current, forwardFrom, forwardTo, currentTimeMs, faults))
            {
                yield return edge;
            }
        }

        internal static bool IsRelayClosed(
            RelayElementDefinition relay,
            IReadOnlyDictionary<string, object?>? signalState,
            IReadOnlyDictionary<string, long>? signalTimes,
            long currentTimeMs,
            SimulationFaultSet faults)
        {
            var forcedState = faults.TryGetForcedRelayState(relay.Id);
            if (forcedState.HasValue)
            {
                return forcedState.Value;
            }

            var control = signalState != null && signalState.TryGetValue(relay.Coil.Signal, out var value)
                ? value
                : null;
            var numeric = ParseSignalValue(control);
            var delayMs = relay.Metadata.TryGetValue("delay_ms", out var delayText) &&
                          double.TryParse(delayText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDelay)
                ? Math.Max(0L, (long)parsedDelay)
                : 0L;
            if (numeric < relay.Coil.ThresholdV)
            {
                return false;
            }

            if (delayMs <= 0 || signalTimes == null || !signalTimes.TryGetValue(relay.Coil.Signal, out var lastChanged))
            {
                return true;
            }

            return currentTimeMs - lastChanged >= delayMs;
        }

        private static double ParseSignalValue(object? control)
        {
            return control switch
            {
                null => 0d,
                bool b => b ? 1d : 0d,
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0d
            };
        }
    }
}
