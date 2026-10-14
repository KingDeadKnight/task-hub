using System.Reflection;
using System.Text.Json.Serialization;
using TaskHub.Api.Endpoints;
using TaskHub.Infrastructure;

const string CorsPolicy = "web";

var builder = WebApplication.CreateBuilder(args);

// At build time, the OpenAPI document generator (GetDocument.Insider) runs this file and starts the host:
// it must neither touch the database nor synchronize sources.
var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (isOpenApiGeneration)
{
    builder.Configuration["Sync:Enabled"] = "false";
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Numbers are numbers: also keeps the OpenAPI schema from typing them "integer | string".
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddTaskHubInfrastructure(builder.Configuration);

var app = builder.Build();

if (!isOpenApiGeneration)
{
    await app.Services.InitializeTaskHubDatabaseAsync();
}

app.UseCors(CorsPolicy);

app.MapOpenApi();

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth");
api.MapItemsEndpoints();
api.MapSourcesEndpoints();

if (app.Environment.IsDevelopment())
{
    api.MapDebugEndpoints();
}

await app.RunAsync();

internal sealed record HealthResponse(string Status);
