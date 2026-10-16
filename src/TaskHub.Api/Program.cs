using System.Reflection;
using System.Text.Json.Serialization;
using TaskHub.Api;
using TaskHub.Api.Endpoints;
using TaskHub.Infrastructure;

// "dotnet TaskHub.Api.dll --healthcheck": probe used by the container healthcheck (the runtime image has no curl).
if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    return await HealthProbe.RunAsync();
}

const string CorsPolicy = "web";

var builder = WebApplication.CreateBuilder(args);

// At build time, the OpenAPI document generator (GetDocument.Insider) runs this file and starts the host:
// it must neither touch the database nor synchronize sources.
var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (isOpenApiGeneration)
{
    builder.Configuration["Sync:Enabled"] = "false";
}

// One JSON object per line in production (collected as-is by `docker logs`); readable console output in development.
if (!builder.Environment.IsDevelopment() && !isOpenApiGeneration)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.IncludeScopes = true;
    });
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

// In the Docker image, the built front end lives in wwwroot (in dev, Vite serves it instead).
// Static files must run before routing: otherwise the SPA fallback endpoint is selected first
// and the static file middleware skips the request (assets would be answered with index.html).
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseCors(CorsPolicy);

app.MapOpenApi();

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapItemsEndpoints();
api.MapSourcesEndpoints();

if (app.Environment.IsDevelopment())
{
    api.MapDebugEndpoints();
}

// Client-side routes fall back to the SPA; unknown /api and /openapi paths stay 404.
app.MapFallbackToFile("{*path:regex(^(?!api/|openapi/).*$)}", "index.html");

await app.RunAsync();
return 0;
