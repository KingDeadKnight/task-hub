using System.Text.Json.Serialization;
using TaskHub.Api.Endpoints;
using TaskHub.Infrastructure;

const string CorsPolicy = "web";

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddTaskHubInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseCors(CorsPolicy);

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")));

if (app.Environment.IsDevelopment())
{
    api.MapDebugEndpoints();
}

app.Run();

internal sealed record HealthResponse(string Status);
