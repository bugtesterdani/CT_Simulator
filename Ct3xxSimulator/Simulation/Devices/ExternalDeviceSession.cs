using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using Ct3xxSimulator.Simulation.WireViz;
using Ct3xxSimulator.Simulation.Waveforms;

namespace Ct3xxSimulator.Simulation.Devices;

internal sealed class ExternalDeviceSession : IDisposable
{
    private readonly PythonDeviceSimulatorClient _client;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private static readonly HashSet<string> GenericPortLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "IN",
        "OUT",
        "GND",
        "VPLUS",
        "VMINUS",
        "VIN",
        "VOUT",
        "SIG"
    };

    public ExternalDeviceSession(PythonDeviceSimulatorClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void Dispose() => _client.Dispose();

    public long CurrentSimTimeMs => _stopwatch.ElapsedMilliseconds;

    public ExternalDeviceResponse Hello(System.Threading.CancellationToken cancellationToken, long? simTimeMs = null) =>
        _client.Hello(simTimeMs ?? CurrentSimTimeMs, cancellationToken);

    public ExternalDeviceResponse Shutdown(System.Threading.CancellationToken cancellationToken, long? simTimeMs = null) =>
        _client.Shutdown(simTimeMs ?? CurrentSimTimeMs, cancellationToken);

    public bool TryReadState(System.Threading.CancellationToken cancellationToken, out ExternalDeviceStateSnapshot snapshot, out string? error, long? simTimeMs = null)
    {
        var response = _client.ReadState(simTimeMs ?? CurrentSimTimeMs, cancellationToken);
        if (!response.Ok)
        {
            snapshot = ExternalDeviceStateSnapshot.Empty;
            error = $"{response.ErrorCode}: {response.ErrorMessage}";
            return false;
        }

        snapshot = ParseStateSnapshot(response.Result as JsonObject);
        error = null;
        return true;
    }

    public bool TryWriteSignal(string name, object? value, System.Threading.CancellationToken cancellationToken, out string? error, long? simTimeMs = null)
    {
        var response = _client.SetInput(name, value, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
        if (!response.Ok)
        {
            error = $"{response.ErrorCode}: {response.ErrorMessage}";
            return false;
        }

        error = null;
        return true;
    }

    public bool TrySetWaveform(string name, AppliedWaveform waveform, object? options, System.Threading.CancellationToken cancellationToken, out JsonObject? result, out string? error, long? simTimeMs = null)
    {
        var response = _client.SetWaveform(name, waveform, options, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
        if (!response.Ok)
        {
            result = null;
            error = $"{response.ErrorCode}: {response.ErrorMessage}";
            return false;
        }

        result = response.Result as JsonObject;
        error = null;
        return true;
    }

    public bool TryReadWaveform(string name, object? options, System.Threading.CancellationToken cancellationToken, out JsonObject? result, out string? error, long? simTimeMs = null)
    {
        var response = _client.ReadWaveform(name, options, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
        if (!response.Ok)
        {
            result = null;
            error = $"{response.ErrorCode}: {response.ErrorMessage}";
            return false;
        }

        result = response.Result as JsonObject;
        error = null;
        return true;
    }

    public bool TryReadSignal(IEnumerable<WireVizConnectionResolution> resolutions, System.Threading.CancellationToken cancellationToken, out string signalName, out object? value, out string? stateSummary, out string? error, long? simTimeMs = null)
    {
        foreach (var candidate in SelectPreferredTargetSignals(resolutions))
        {
            var response = _client.GetSignal(candidate, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
            if (!response.Ok)
            {
                error = $"{response.ErrorCode}: {response.ErrorMessage}";
                signalName = candidate;
                value = null;
                stateSummary = FormatState(response);
                return false;
            }

            var resultObject = response.Result?.AsObject();
            if (resultObject?["value"] == null)
            {
                continue;
            }

            signalName = candidate;
            value = ExtractNodeValue(resultObject["value"]);
            stateSummary = FormatState(response);
            error = null;
            return true;
        }

        signalName = string.Empty;
        value = null;
        stateSummary = null;
        error = null;
        return false;
    }

    public bool TryReadSignal(IEnumerable<WireVizRuntimeTarget> targets, System.Threading.CancellationToken cancellationToken, out string signalName, out object? value, out string? stateSummary, out string? error, long? simTimeMs = null)
    {
        foreach (var target in targets)
        {
            var response = _client.GetSignal(target.SignalName, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
            if (!response.Ok)
            {
                error = $"{response.ErrorCode}: {response.ErrorMessage}";
                signalName = target.SignalName;
                value = null;
                stateSummary = FormatState(response);
                return false;
            }

            var resultObject = response.Result?.AsObject();
            if (resultObject?["value"] == null)
            {
                continue;
            }

            signalName = target.SignalName;
            value = target.ApplyRead(ExtractNodeValue(resultObject["value"]));
            stateSummary = FormatState(response);
            error = null;
            return true;
        }

        signalName = string.Empty;
        value = null;
        stateSummary = null;
        error = null;
        return false;
    }

    public bool TryWriteSignal(IEnumerable<WireVizConnectionResolution> resolutions, object? value, System.Threading.CancellationToken cancellationToken, out IReadOnlyList<string> writtenSignals, out string? error, long? simTimeMs = null)
    {
        var targets = SelectPreferredTargetSignals(resolutions).ToList();
        if (targets.Count == 0)
        {
            writtenSignals = Array.Empty<string>();
            error = null;
            return false;
        }

        var written = new List<string>();
        foreach (var target in targets)
        {
            var response = _client.SetInput(target, value, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
            if (!response.Ok)
            {
                writtenSignals = written;
                error = $"{target}: {response.ErrorCode}: {response.ErrorMessage}";
                return written.Count > 0;
            }

            written.Add(target);
        }

        writtenSignals = written;
        error = null;
        return written.Count > 0;
    }

    public bool TryWriteSignal(IEnumerable<WireVizRuntimeTarget> targets, object? value, System.Threading.CancellationToken cancellationToken, out IReadOnlyList<string> writtenSignals, out string? error, long? simTimeMs = null)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
        {
            writtenSignals = Array.Empty<string>();
            error = null;
            return false;
        }

        var written = new List<string>();
        foreach (var target in targetList)
        {
            var targetValue = target.ApplyWrite(value);
            var response = _client.SetInput(target.SignalName, targetValue, simTimeMs ?? CurrentSimTimeMs, cancellationToken);
            if (!response.Ok)
            {
                writtenSignals = written;
                error = $"{target.SignalName}: {response.ErrorCode}: {response.ErrorMessage}";
                return written.Count > 0;
            }

            written.Add(target.SignalName);
        }

        writtenSignals = written;
        error = null;
        return written.Count > 0;
    }

    private static IEnumerable<string> SelectPreferredTargetSignals(IEnumerable<WireVizConnectionResolution> resolutions)
    {
        return resolutions
            .SelectMany(resolution => resolution.Targets)
            .OrderBy(endpoint => endpoint.Role == Ct3xxWireVizParser.Model.WireVizConnectorRole.Device ? 0 : endpoint.Role == Ct3xxWireVizParser.Model.WireVizConnectorRole.Harness ? 1 : 2)
            .ThenByDescending(endpoint => endpoint.Key.Count(ch => ch == '.'))
            .ThenBy(endpoint => IsGenericBoundaryLabel(endpoint.PinLabel) ? 1 : 0)
            .Select(endpoint => string.IsNullOrWhiteSpace(endpoint.PinLabel) ? endpoint.Key : endpoint.PinLabel!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsGenericBoundaryLabel(string? label)
    {
        return !string.IsNullOrWhiteSpace(label) && GenericPortLabels.Contains(label.Trim());
    }

    private static object? ExtractNodeValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonObject objectNode)
        {
            return objectNode;
        }

        if (node is JsonArray arrayNode)
        {
            return arrayNode;
        }

        if (node is System.Text.Json.Nodes.JsonValue valueNode)
        {
            if (valueNode.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (valueNode.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (valueNode.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (valueNode.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }

            if (valueNode.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }
        }

        return node.ToJsonString();
    }

    private static string? FormatState(ExternalDeviceResponse response)
    {
        return response.StateAtRequest?.ToJsonString();
    }

    private static ExternalDeviceStateSnapshot ParseStateSnapshot(JsonObject? payload)
    {
        if (payload == null)
        {
            return ExternalDeviceStateSnapshot.Empty;
        }

        return new ExternalDeviceStateSnapshot(
            payload["time_ms"]?.GetValue<long?>() ?? 0L,
            ReadStringMap(payload["inputs"] as JsonObject),
            ReadStringMap(payload["sources"] as JsonObject),
            ReadStringMap(payload["internal"] as JsonObject),
            ReadStringMap(payload["outputs"] as JsonObject),
            ReadStringMap(payload["interfaces"] as JsonObject));
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonObject? values)
    {
        if (values == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            if (item.Key == null)
            {
                continue;
            }

            result[item.Key] = item.Value == null ? string.Empty : ExtractNodeValue(item.Value)?.ToString() ?? item.Value.ToJsonString();
        }

        return result;
    }
}
