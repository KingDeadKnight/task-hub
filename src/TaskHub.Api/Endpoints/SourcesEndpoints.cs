using Microsoft.AspNetCore.Http.HttpResults;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Infrastructure.Queries;
using TaskHub.Infrastructure.Sync;

namespace TaskHub.Api.Endpoints;

internal static class SourcesEndpoints
{
    public static RouteGroupBuilder MapSourcesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sources", GetSourcesAsync)
            .WithName("GetSources")
            .WithSummary("État de chaque source : dernière synchro, succès ou erreur, nombre d'éléments actifs.");

        api.MapPost("/sync", SyncAsync)
            .WithName("Sync")
            .WithSummary("Lance immédiatement une synchronisation de toutes les sources et attend sa fin.")
            .WithDescription("Répond 409 si une synchronisation est déjà en cours.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }

    private static async Task<Ok<SourcesStatusDto>> GetSourcesAsync(
        IEnumerable<ITaskSource> sources,
        WorkItemQueryService queries,
        SyncRunner runner,
        CancellationToken ct)
    {
        var summaries = await queries.GetSourceSummariesAsync(sources.Select(s => s.Name), ct);
        return TypedResults.Ok(new SourcesStatusDto(runner.IsRunning, summaries.Select(SourceStatusDto.From).ToList()));
    }

    private static async Task<Results<Ok<SyncResponseDto>, ProblemHttpResult>> SyncAsync(SyncRunner runner, CancellationToken ct)
    {
        if (runner.IsRunning)
        {
            return TypedResults.Problem(
                "Une synchronisation est déjà en cours.", statusCode: StatusCodes.Status409Conflict, title: "Synchronisation en cours");
        }

        var results = await runner.RunAllAsync(ct);
        return TypedResults.Ok(SyncResponseDto.From(results));
    }
}
