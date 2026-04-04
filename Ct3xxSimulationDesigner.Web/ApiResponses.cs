namespace Ct3xxSimulationDesigner.Web;

/// <summary>
/// Defines API request/response payloads.
/// </summary>
public static class ApiResponses
{
    /// <summary>
    /// Wraps a successful response.
    /// </summary>
    public static ApiResponse<T> ApiOk<T>(T payload) => new("1.0", true, payload, null);

    /// <summary>
    /// Wraps an error response.
    /// </summary>
    public static ApiResponse<object> ApiError(string code, string message) =>
        new("1.0", false, null, new ApiErrorResponse(code, message));
}

/// <summary>
/// Simulation import request.
/// </summary>
public record ImportRequest(string Yaml);

/// <summary>
/// Simulation export request.
/// </summary>
public record ExportRequest(object? Data);

/// <summary>
/// Response container for YAML exports.
/// </summary>
public record ExportResponse(string Content);

/// <summary>
/// API response wrapper.
/// </summary>
public record ApiResponse<T>(string Version, bool Success, T? Data, ApiErrorResponse? Error);

/// <summary>
/// API error payload.
/// </summary>
public record ApiErrorResponse(string Code, string Message);
