using Microsoft.AspNetCore.Http.HttpResults;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources;

namespace TaskHub.Api.Endpoints;

/// <summary>Development-only endpoints that call a source live, bypassing storage.</summary>
internal static class DebugEndpoints
{
    public static RouteGroupBuilder MapDebugEndpoints(this RouteGroupBuilder api)
    {
        // e.g. GET /api/debug/azuredevops
        api.MapGet("/debug/{source}", FetchFromSourceAsync).ExcludeFromDescription();
        return api;
    }

    private static async Task<Results<Ok<IReadOnlyList<WorkItem>>, NotFound, ProblemHttpResult>> FetchFromSourceAsync(
        string source,
        IEnumerable<ITaskSource> sources,
        CancellationToken ct)
    {
        var taskSource = sources.FirstOrDefault(s => string.Equals(s.Name, source, StringComparison.OrdinalIgnoreCase));
        if (taskSource is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            return TypedResults.Ok(await taskSource.FetchAssignedToMeAsync(ct));
        }
        catch (SourceNotConfiguredException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Source non configurée");
        }
        catch (SourceRequestException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Erreur de la source");
        }
    }
}
