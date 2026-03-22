using System.Text.Json.Nodes;

namespace Ct3xxSimulator.Simulation.Devices;

internal sealed class ExternalDeviceResponse
{
    public ExternalDeviceResponse(bool ok, long? simTimeMs, JsonObject? stateAtRequest, JsonNode? result, string? errorCode, string? errorMessage)
    {
        Ok = ok;
        SimTimeMs = simTimeMs;
        StateAtRequest = stateAtRequest;
        Result = result;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool Ok { get; }
    public long? SimTimeMs { get; }
    public JsonObject? StateAtRequest { get; }
    public JsonNode? Result { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
}
