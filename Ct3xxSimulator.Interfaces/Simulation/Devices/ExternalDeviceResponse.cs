using System.Text.Json.Nodes;

namespace Ct3xxSimulator.Simulation.Devices;

/// <summary>
/// Represents one response returned by the external device process over the pipe protocol.
/// </summary>
public sealed class ExternalDeviceResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalDeviceResponse"/> class.
    /// </summary>
    /// <param name="ok">Indicates whether the external device handled the request successfully.</param>
    /// <param name="simTimeMs">The simulated time reported by the device for the request.</param>
    /// <param name="stateAtRequest">The device state recorded when the request was handled.</param>
    /// <param name="result">The optional result payload.</param>
    /// <param name="errorCode">The protocol-level error code.</param>
    /// <param name="errorMessage">The protocol-level error message.</param>
    public ExternalDeviceResponse(bool ok, long? simTimeMs, JsonObject? stateAtRequest, JsonNode? result, string? errorCode, string? errorMessage)
    {
        Ok = ok;
        SimTimeMs = simTimeMs;
        StateAtRequest = stateAtRequest;
        Result = result;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    public bool Ok { get; }
    /// <summary>
    /// Gets the simulated time reported by the device for the request.
    /// </summary>
    public long? SimTimeMs { get; }
    /// <summary>
    /// Gets the device state captured while the request was processed.
    /// </summary>
    public JsonObject? StateAtRequest { get; }
    /// <summary>
    /// Gets the optional result payload of the request.
    /// </summary>
    public JsonNode? Result { get; }
    /// <summary>
    /// Gets the error code reported by the device when the request failed.
    /// </summary>
    public string? ErrorCode { get; }
    /// <summary>
    /// Gets the error message reported by the device when the request failed.
    /// </summary>
    public string? ErrorMessage { get; }
}
