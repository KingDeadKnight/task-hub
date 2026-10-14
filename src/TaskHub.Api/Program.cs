const string CorsPolicy = "web";

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseCors(CorsPolicy);

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")));

app.Run();

internal sealed record HealthResponse(string Status);
