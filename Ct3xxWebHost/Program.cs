using Ct3xxSimulationModelParser.Parsing;
using Ct3xxSimulationSchema;
using Ct3xxWireVizDesigner.Core.Model;
using Ct3xxWireVizDesigner.Core.Serialization;
using Ct3xxWireVizDesigner.Core.WireViz;
using Ct3xxWireVizSchema;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using YamlDotNet.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "wireviz")),
    RequestPath = "/wireviz"
});
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "simulation")),
    RequestPath = "/simulation"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "wireviz")),
    RequestPath = "/wireviz",
    OnPrepareResponse = DisableCache
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "simulation")),
    RequestPath = "/simulation",
    OnPrepareResponse = DisableCache
});

app.MapGet("/health", () => Results.Ok("ok"));

var wireviz = app.MapGroup("/wireviz");
wireviz.MapGet("/api/wireviz/schema", () =>
{
    return Results.Ok(ApiResponses.ApiOk(new
    {
        connectorKeys = WireVizSchema.ConnectorKeys,
        cableKeys = WireVizSchema.CableKeys,
        propertyTypeHints = WireVizSchema.PropertyTypeHints,
        referenceCommit = WireVizSchema.ReferenceCommit
    }));
});

wireviz.MapPost("/api/wireviz/import", (ImportRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        return Results.BadRequest(ApiResponses.ApiError("yaml_required", "YAML is required."));
    }

    var graph = WireVizBlockMapper.ImportFromYaml(request.Yaml);
    return Results.Ok(ApiResponses.ApiOk(graph));
});

wireviz.MapPost("/api/wireviz/export", (ExportWireVizRequest request) =>
{
    if (request.Graph is null)
    {
        return Results.BadRequest(ApiResponses.ApiError("graph_required", "Graph is required."));
    }
    var yaml = WireVizBlockMapper.ExportToYaml(request.Graph, request.Full);
    return Results.Ok(ApiResponses.ApiOk(new ExportResponse(yaml)));
});

wireviz.MapPost("/api/graph/deserialize", (JsonElement payload) =>
{
    var json = payload.GetRawText();
    var graph = BlockGraphJson.Deserialize(json);
    return Results.Ok(ApiResponses.ApiOk(graph));
});

wireviz.MapPost("/api/graph/serialize", (BlockGraph graph) =>
{
    var json = BlockGraphJson.Serialize(graph);
    return Results.Ok(ApiResponses.ApiOk(new ExportResponse(json)));
});

var simulation = app.MapGroup("/simulation");
simulation.MapGet("/api/simulation/schema", () =>
{
    var root = app.Environment.ContentRootPath;
    var repoRoot = FindRepoRoot(root);
    var schema = new SimulationSchemaBuilder().Build(repoRoot);
    if (schema.ElementTypes.Count <= 1)
    {
        schema.ApplyDefaults();
    }

    return Results.Ok(ApiResponses.ApiOk(new
    {
        TopLevelKeys = schema.TopLevelKeys.OrderBy(x => x).ToArray(),
        schema.SectionKeys,
        ElementTypes = schema.ElementTypes.OrderBy(x => x).ToArray(),
        schema.ElementTypeKeys,
        schema.PropertyValueHints,
        schema.RequiredElementTypeKeys,
        schema.ElementTypeHelp,
        schema.FreeFormElementTypes,
        schema.GenericElementOptionalKeys,
        schema.ElementFieldTemplates,
        schema.ElementFieldEditors
    }));
});

simulation.MapPost("/api/simulation/parse", (ImportRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        return Results.BadRequest(ApiResponses.ApiError("yaml_required", "YAML is required."));
    }

    try
    {
        var parser = new SimulationModelParser();
        var doc = parser.ParseText(request.Yaml, "<inline>");
        var elements = doc.Elements.Select(element => new
        {
            element.Id,
            element.Type,
            element.Metadata
        });
        return Results.Ok(ApiResponses.ApiOk(new { elements }));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponses.ApiError("parse_failed", ex.Message));
    }
});

simulation.MapPost("/api/simulation/import", (ImportRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        return Results.BadRequest(ApiResponses.ApiError("yaml_required", "YAML is required."));
    }

    var deserializer = new DeserializerBuilder().Build();
    var data = deserializer.Deserialize<object>(request.Yaml);
    return Results.Ok(ApiResponses.ApiOk(data));
});

simulation.MapPost("/api/simulation/export", (ExportRequest request) =>
{
    if (request.Data is null)
    {
        return Results.BadRequest(ApiResponses.ApiError("data_required", "Data is required."));
    }

    var serializer = new SerializerBuilder().Build();
    var data = NormalizeForYaml(request.Data);
    var yaml = serializer.Serialize(data);
    return Results.Ok(ApiResponses.ApiOk(new { content = yaml }));
});

app.MapFallbackToFile("/wireviz/{*path:nonfile}", "wireviz/index.html");
app.MapFallbackToFile("/simulation/{*path:nonfile}", "simulation/index.html");

app.Run();

static void DisableCache(StaticFileResponseContext context)
{
    context.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
    context.Context.Response.Headers["Pragma"] = "no-cache";
    context.Context.Response.Headers["Expires"] = "0";
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "CT3xx.sln")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return start;
}

static object? NormalizeForYaml(object? data)
{
    return data is JsonElement element
        ? ConvertJsonElement(element)
        : data;
}

static object? ConvertJsonElement(JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = ConvertJsonElement(prop.Value);
            }
            return dict;
        }
        case JsonValueKind.Array:
        {
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(ConvertJsonElement(item));
            }
            return list;
        }
        case JsonValueKind.String:
            return element.GetString();
        case JsonValueKind.Number:
            if (element.TryGetInt64(out var longValue))
            {
                return longValue;
            }
            return element.GetDouble();
        case JsonValueKind.True:
            return true;
        case JsonValueKind.False:
            return false;
        case JsonValueKind.Null:
        case JsonValueKind.Undefined:
        default:
            return null;
    }
}

public static class ApiResponses
{
    public static ApiResponse<T> ApiOk<T>(T payload) => new("1.0", true, payload, null);

    public static ApiResponse<object> ApiError(string code, string message) =>
        new("1.0", false, null, new ApiErrorResponse(code, message));
}

public record ImportRequest(string Yaml);
public record ExportResponse(string Content);
public record ExportRequest(object? Data);
public record ExportWireVizRequest(BlockGraph? Graph, bool Full);
public record ApiResponse<T>(string Version, bool Success, T? Data, ApiErrorResponse? Error);
public record ApiErrorResponse(string Code, string Message);
