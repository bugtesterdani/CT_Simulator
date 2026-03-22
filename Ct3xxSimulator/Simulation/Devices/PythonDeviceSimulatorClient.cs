using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Ct3xxSimulator.Simulation.Devices;

internal sealed class PythonDeviceSimulatorClient : IDisposable
{
    private readonly string _pipeName;
    private readonly object _sync = new();
    private NamedPipeClientStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private int _requestCounter;

    public PythonDeviceSimulatorClient(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name must be provided.", nameof(pipeName));
        }

        _pipeName = NormalizePipeName(pipeName);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeConnection();
        }
    }

    public ExternalDeviceResponse Hello(long simTimeMs, CancellationToken cancellationToken) =>
        Send("hello", null, simTimeMs, cancellationToken);

    public ExternalDeviceResponse SetInput(string name, object? value, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name,
            ["value"] = value == null ? null : JsonValue.Create(value)
        };

        return Send("set_input", payload, simTimeMs, cancellationToken);
    }

    public ExternalDeviceResponse GetSignal(string name, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name
        };

        return Send("get_signal", payload, simTimeMs, cancellationToken);
    }

    public ExternalDeviceResponse ReadState(long simTimeMs, CancellationToken cancellationToken) =>
        Send("read_state", null, simTimeMs, cancellationToken);

    private ExternalDeviceResponse Send(string action, JsonObject? payload, long simTimeMs, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureConnected(cancellationToken);

            var request = payload ?? new JsonObject();
            request["id"] = $"req-{Interlocked.Increment(ref _requestCounter).ToString(CultureInfo.InvariantCulture)}";
            request["action"] = action;
            request["sim_time_ms"] = simTimeMs;

            var json = request.ToJsonString();
            _writer!.WriteLine(json);
            _writer.Flush();

            var line = _reader!.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new IOException("Pipe returned an empty response.");
            }

            var responseJson = JsonNode.Parse(line)?.AsObject()
                ?? throw new IOException("Pipe returned invalid JSON.");

            var ok = responseJson["ok"]?.GetValue<bool>() ?? false;
            var result = responseJson["result"];
            var stateAtRequest = responseJson["state_at_request"] as JsonObject;
            var error = responseJson["error"] as JsonObject;
            var errorCode = error?["code"]?.GetValue<string>();
            var errorMessage = error?["message"]?.GetValue<string>();
            var responseTime = responseJson["sim_time_ms"]?.GetValue<long?>();

            return new ExternalDeviceResponse(ok, responseTime, stateAtRequest, result, errorCode, errorMessage);
        }
    }

    private void EnsureConnected(CancellationToken cancellationToken)
    {
        if (_stream is { IsConnected: true })
        {
            return;
        }

        DisposeConnection();

        var (serverName, pipeName) = SplitPipeName(_pipeName);
        var stream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.None);
        stream.Connect(1000);
        cancellationToken.ThrowIfCancellationRequested();

        _stream = stream;
        _reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    private void DisposeConnection()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _writer = null;
        _reader = null;
        _stream = null;
    }

    private static string NormalizePipeName(string pipeName)
    {
        var trimmed = pipeName.Trim();
        return trimmed.StartsWith(@"\\", StringComparison.Ordinal)
            ? trimmed
            : $@"\\.\pipe\{trimmed}";
    }

    private static (string ServerName, string PipeName) SplitPipeName(string pipePath)
    {
        const string prefix = @"\\";
        if (!pipePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"Invalid pipe path '{pipePath}'.");
        }

        var parts = pipePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !string.Equals(parts[1], "pipe", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"Invalid pipe path '{pipePath}'.");
        }

        return (parts[0], parts[2]);
    }
}
