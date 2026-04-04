using Ct3xxSimulationSchema;
using Ct3xxSimulationModelParser.Parsing;
using YamlDotNet.Serialization;
using Ct3xxSimulationDesigner.Web;
using Ct3xxSimulationModelParser.Model;
using System.Linq;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        context.Context.Response.Headers["Pragma"] = "no-cache";
        context.Context.Response.Headers["Expires"] = "0";
    }
});

app.MapGet("/health", () => Results.Ok("ok"));

app.MapGet("/api/simulation/schema", () =>
{
    var root = AppContext.BaseDirectory;
    var repoRoot = FindRepoRoot(root);
    var builder = new SimulationSchemaBuilder();
    var schema = builder.Build(repoRoot);
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

app.MapPost("/api/simulation/parse", (ImportRequest request) =>
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

app.MapPost("/api/simulation/import", (ImportRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        return Results.BadRequest(ApiResponses.ApiError("yaml_required", "YAML is required."));
    }

    var deserializer = new DeserializerBuilder().Build();
    var data = deserializer.Deserialize<object>(request.Yaml);
    return Results.Ok(ApiResponses.ApiOk(data));
});

app.MapPost("/api/simulation/export", (ExportRequest request) =>
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

app.UseSwagger();
app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "CT3xx Simulation Designer API"); });

app.Run();

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
