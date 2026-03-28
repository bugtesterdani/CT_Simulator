using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Ct3xxSimulator.Simulation.Waveforms;

namespace Ct3xxSimulator.Simulation.Devices;

/// <summary>
/// Sends low-level pipe protocol requests to the Python device simulator process.
/// </summary>
public sealed class PythonDeviceSimulatorClient : IDisposable
{
    private readonly string _pipeName;
    private readonly object _sync = new();
    private NamedPipeClientStream? _stream;
    private int _requestCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonDeviceSimulatorClient"/> class.
    /// </summary>
    /// <param name="pipeName">The full pipe path or logical pipe name to connect to.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pipeName"/> is missing.</exception>
    public PythonDeviceSimulatorClient(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name must be provided.", nameof(pipeName));
        }

        _pipeName = NormalizePipeName(pipeName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            DisposeConnection();
        }
    }

    /// <summary>
    /// Sends the protocol handshake command to the Python device.
    /// </summary>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse Hello(long simTimeMs, CancellationToken cancellationToken) =>
        Send("hello", null, simTimeMs, cancellationToken);

    /// <summary>
    /// Writes one logical input value to the Python device.
    /// </summary>
    /// <param name="name">The logical signal name.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse SetInput(string name, object? value, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name,
            ["value"] = ToJsonNode(value)
        };

        return Send("set_input", payload, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Reads one logical signal value from the Python device.
    /// </summary>
    /// <param name="name">The logical signal name.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse GetSignal(string name, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name
        };

        return Send("get_signal", payload, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Reads the full published device state.
    /// </summary>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse ReadState(long simTimeMs, CancellationToken cancellationToken) =>
        Send("read_state", null, simTimeMs, cancellationToken);

    /// <summary>
    /// Sends one interface payload to the Python device.
    /// </summary>
    /// <param name="name">The logical interface name.</param>
    /// <param name="payload">The interface payload.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse SendInterface(string name, object? payload, long simTimeMs, CancellationToken cancellationToken)
    {
        var request = new JsonObject
        {
            ["name"] = name,
            ["payload"] = ToJsonNode(payload)
        };

        return Send("send_interface", request, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Applies a waveform stimulus to the Python device.
    /// </summary>
    /// <param name="name">The logical signal name that receives the waveform.</param>
    /// <param name="waveform">The waveform to apply.</param>
    /// <param name="options">Optional waveform application options.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse SetWaveform(string name, AppliedWaveform waveform, object? options, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name,
            ["waveform"] = JsonSerializer.SerializeToNode(new
            {
                signal = waveform.SignalName,
                name = waveform.WaveformName,
                sample_time_ms = waveform.SampleTimeMs,
                delay_ms = waveform.DelayMs,
                periodic = waveform.Periodic,
                cycles = waveform.Cycles,
                points = waveform.Points.Select(point => new { time_ms = point.TimeMs, value = point.Value }).ToArray(),
                metadata = waveform.Metadata
            }),
            ["options"] = ToJsonNode(options)
        };

        return Send("set_waveform", payload, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Reads a waveform or waveform capture back from the Python device.
    /// </summary>
    /// <param name="name">The logical signal name to read.</param>
    /// <param name="options">Optional waveform read options.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse ReadWaveform(string name, object? options, long simTimeMs, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["name"] = name,
            ["options"] = ToJsonNode(options)
        };

        return Send("read_waveform", payload, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Reads the latest payload available on one logical interface.
    /// </summary>
    /// <param name="name">The logical interface name.</param>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse ReadInterface(string name, long simTimeMs, CancellationToken cancellationToken)
    {
        var request = new JsonObject
        {
            ["name"] = name
        };

        return Send("read_interface", request, simTimeMs, cancellationToken);
    }

    /// <summary>
    /// Requests a graceful shutdown of the Python device process.
    /// </summary>
    /// <param name="simTimeMs">The current simulated time in milliseconds.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The raw device response.</returns>
    public ExternalDeviceResponse Shutdown(long simTimeMs, CancellationToken cancellationToken) =>
        Send("shutdown", null, simTimeMs, cancellationToken);

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

            WriteMessage(_stream!, request.ToJsonString());
            var responseText = ReadMessage(_stream!);
            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new IOException("Pipe returned an empty response.");
            }

            var responseJson = JsonNode.Parse(responseText)?.AsObject()
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
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                stream.Connect(250);
                break;
            }
            catch (TimeoutException) when (DateTime.UtcNow < deadline)
            {
            }
        }

        _stream = stream;
    }

    private void DisposeConnection()
    {
        _stream?.Dispose();
        _stream = null;
    }

    private static void WriteMessage(Stream stream, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        Span<byte> lengthPrefix = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, payload.Length);
        stream.Write(lengthPrefix);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static string ReadMessage(Stream stream)
    {
        Span<byte> lengthPrefix = stackalloc byte[4];
        ReadExact(stream, lengthPrefix);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);
        if (length <= 0)
        {
            throw new IOException("Pipe returned an invalid response length.");
        }

        var payload = new byte[length];
        ReadExact(stream, payload);
        return Encoding.UTF8.GetString(payload);
    }

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer[offset..]);
            if (read <= 0)
            {
                throw new IOException("Pipe connection was closed while reading.");
            }

            offset += read;
        }
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

    private static JsonNode? ToJsonNode(object? value)
    {
        return value == null ? null : JsonSerializer.SerializeToNode(value);
    }
}
