using Microsoft.AspNetCore.Http.HttpResults;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Infrastructure.Health;

namespace TaskHub.Api.Endpoints;

internal static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithSummary("Santé : base de données joignable et dernière synchro réussie assez récente.")
            .WithDescription("Répond 503 (status « unhealthy ») si la base ne répond pas ou si la synchro n'a pas réussi depuis 3 intervalles.");

        return api;
    }

    private static async Task<Results<Ok<HealthDto>, JsonHttpResult<HealthDto>>> GetHealthAsync(
        HealthService health,
        IEnumerable<ITaskSource> sources,
        CancellationToken ct)
    {
        var report = await health.CheckAsync(sources.Any(s => s.IsConfigured), ct);
        var healthy = report.State == HealthState.Healthy;
        var dto = new HealthDto(healthy ? "ok" : "unhealthy", report.DatabaseReachable, report.LastSuccessfulSyncAt, report.SyncStale);
        return healthy
            ? TypedResults.Ok(dto)
            : TypedResults.Json(dto, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
