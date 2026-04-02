using Ct3xxWireVizDesigner.Core.Model;
using Ct3xxWireVizDesigner.Core.Serialization;
using Ct3xxWireVizDesigner.Core.WireViz;
using Ct3xxWireVizDesigner.Web;
using Ct3xxWireVizSchema;
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
app.MapGet("/api/wireviz/schema", () =>
{
    return Results.Ok(ApiResponses.ApiOk(new
    {
        connectorKeys = WireVizSchema.ConnectorKeys,
        cableKeys = WireVizSchema.CableKeys,
        propertyTypeHints = WireVizSchema.PropertyTypeHints,
        referenceCommit = WireVizSchema.ReferenceCommit
    }));
});

app.MapPost("/api/wireviz/import", (ImportRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Yaml))
    {
        return Results.BadRequest(ApiResponses.ApiError("yaml_required", "YAML is required."));
    }

    var graph = WireVizBlockMapper.ImportFromYaml(request.Yaml);
    return Results.Ok(ApiResponses.ApiOk(graph));
});

app.MapPost("/api/wireviz/export", (ExportWireVizRequest request) =>
{
    if (request.Graph is null)
    {
        return Results.BadRequest(ApiResponses.ApiError("graph_required", "Graph is required."));
    }
    var yaml = WireVizBlockMapper.ExportToYaml(request.Graph, request.Full);
    return Results.Ok(ApiResponses.ApiOk(new ExportResponse(yaml)));
});

app.MapPost("/api/graph/deserialize", (JsonElement payload) =>
{
    var json = payload.GetRawText();
    var graph = BlockGraphJson.Deserialize(json);
    return Results.Ok(ApiResponses.ApiOk(graph));
});

app.MapPost("/api/graph/serialize", (BlockGraph graph) =>
{
    var json = BlockGraphJson.Serialize(graph);
    return Results.Ok(ApiResponses.ApiOk(new ExportResponse(json)));
});

app.UseSwagger();
app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "CT3xx WireViz Designer API"); });

app.Run();
