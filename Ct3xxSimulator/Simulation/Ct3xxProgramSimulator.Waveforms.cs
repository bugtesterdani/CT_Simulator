// Provides Ct3xx Program Simulator Waveforms for the simulator core simulation support.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public partial class Ct3xxProgramSimulator
{
    private string ResolveWaveformRuntimeSignal(string fallbackSignal, IReadOnlyDictionary<string, string> metadata, string mbusKey, string testPointKey)
    {
        metadata.TryGetValue("CHANNEL_CARD_INDEX", out var cardIndex);
        if (metadata.TryGetValue(mbusKey, out var busValue) &&
            !string.IsNullOrWhiteSpace(busValue) &&
            _measurementBusSignals.TryGetValue($"MBus{busValue.Trim()}", out var mappedSignal) &&
            !string.IsNullOrWhiteSpace(mappedSignal))
        {
            return string.IsNullOrWhiteSpace(cardIndex) ? mappedSignal! : $"AM2/{cardIndex} {mappedSignal}";
        }

        if (metadata.TryGetValue(testPointKey, out var testPoint) && !string.IsNullOrWhiteSpace(testPoint))
        {
            return string.IsNullOrWhiteSpace(cardIndex) ? testPoint.Trim() : $"AM2/{cardIndex} {testPoint.Trim()}";
        }

        return fallbackSignal;
    }

    private string ResolveExternalWaveformTarget(string signalName, bool write)
    {
        if (_wireVizResolver != null &&
            _wireVizResolver.TryResolveRuntimeTargets(signalName, _signalState, _signalChangedAtMs, _simulatedTimeMs, _faults, write, out var runtimeTargets))
        {
            var resolved = runtimeTargets.FirstOrDefault()?.SignalName;
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved!;
            }
        }

        return signalName;
    }

    private void LogWaveformResponse(Test test, JsonObject? response)
    {
        var result = UnwrapWaveformResult(response);
        if (result == null)
        {
            return;
        }

        var waveformInfo = result["waveform"] as JsonObject;
        var metrics = waveformInfo?["metrics"] as JsonObject;
        var shape = metrics?["shape"]?.GetValue<string>();
        var peak = metrics?["peak"]?.GetValue<double?>();
        var rms = metrics?["rms"]?.GetValue<double?>();
        if (!string.IsNullOrWhiteSpace(shape))
        {
            _observer.OnMessage($"Waveform '{test.Name ?? test.Id}' erkannt als {shape}, peak={peak?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-"}, rms={rms?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-"}");
        }

        var observed = result["observed"] as JsonObject;
        if (observed != null)
        {
            foreach (var item in observed)
            {
                var value = item.Value?.GetValue<double?>();
                _observer.OnMessage($"Waveform immediate {item.Key} = {value?.ToString("0.###", CultureInfo.InvariantCulture) ?? item.Value?.ToJsonString() ?? "n/a"}");
            }
        }
    }

    private void TryPublishWaveformVariables(string stimulusSignal, string? observeSignal, JsonObject? response)
    {
        var result = UnwrapWaveformResult(response);
        var waveformInfo = result?["waveform"] as JsonObject;
        var metrics = waveformInfo?["metrics"] as JsonObject;
        if (metrics != null)
        {
            PublishMetricVariable($"{stimulusSignal}_WF_PEAK", metrics["peak"]);
            PublishMetricVariable($"{stimulusSignal}_WF_RMS", metrics["rms"]);
            PublishMetricVariable($"{stimulusSignal}_WF_AVG", metrics["average"]);
        }

        if (string.IsNullOrWhiteSpace(observeSignal))
        {
            return;
        }

        var captures = result?["captures"] as JsonObject;
        var responseCapture = captures?[observeSignal!] as JsonObject;
        var responseMetrics = responseCapture?["metrics"] as JsonObject;
        if (responseMetrics != null)
        {
            PublishMetricVariable("Uave", responseMetrics["average"]);
            PublishMetricVariable("UMeas", responseMetrics["rms"]);
            PublishMetricVariable($"{observeSignal}_WF_PEAK", responseMetrics["peak"]);
            PublishMetricVariable($"{observeSignal}_WF_RMS", responseMetrics["rms"]);
            PublishMetricVariable($"{observeSignal}_WF_AVG", responseMetrics["average"]);
        }
    }

    private static JsonObject? UnwrapWaveformResult(JsonObject? response)
    {
        if (response == null)
        {
            return null;
        }

        if (response["result"] is JsonObject nested)
        {
            return nested;
        }

        return response;
    }

    private void PublishMetricVariable(string variableName, JsonNode? node)
    {
        if (node == null)
        {
            return;
        }

        if (node is JsonValue valueNode && valueNode.TryGetValue<double>(out var numeric))
        {
            _context.SetValue(variableName, numeric);
            _observer.OnMessage($"{variableName} := {numeric.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
    }
}
