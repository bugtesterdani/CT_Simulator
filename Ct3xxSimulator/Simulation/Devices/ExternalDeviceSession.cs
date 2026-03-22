using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ct3xxSimulator.Simulation.WireViz;

namespace Ct3xxSimulator.Simulation.Devices;

internal sealed class ExternalDeviceSession : IDisposable
{
    private readonly PythonDeviceSimulatorClient _client;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public ExternalDeviceSession(PythonDeviceSimulatorClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void Dispose() => _client.Dispose();

    public long CurrentSimTimeMs => _stopwatch.ElapsedMilliseconds;

    public ExternalDeviceResponse Hello(System.Threading.CancellationToken cancellationToken) =>
        _client.Hello(CurrentSimTimeMs, cancellationToken);

    public bool TryReadSignal(IEnumerable<WireVizConnectionResolution> resolutions, System.Threading.CancellationToken cancellationToken, out string signalName, out object? value, out string? stateSummary, out string? error)
    {
        foreach (var candidate in SelectPreferredTargetSignals(resolutions))
        {
            var response = _client.GetSignal(candidate, CurrentSimTimeMs, cancellationToken);
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

    public bool TryWriteSignal(IEnumerable<WireVizConnectionResolution> resolutions, object? value, System.Threading.CancellationToken cancellationToken, out IReadOnlyList<string> writtenSignals, out string? error)
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
            var response = _client.SetInput(target, value, CurrentSimTimeMs, cancellationToken);
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

    private static IEnumerable<string> SelectPreferredTargetSignals(IEnumerable<WireVizConnectionResolution> resolutions)
    {
        return resolutions
            .SelectMany(resolution => resolution.Targets)
            .OrderBy(endpoint => endpoint.Role == Ct3xxWireVizParser.Model.WireVizConnectorRole.Device ? 0 : endpoint.Role == Ct3xxWireVizParser.Model.WireVizConnectorRole.Harness ? 1 : 2)
            .Select(endpoint => string.IsNullOrWhiteSpace(endpoint.PinLabel) ? endpoint.Key : endpoint.PinLabel!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static object? ExtractNodeValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null)
        {
            return null;
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
}
