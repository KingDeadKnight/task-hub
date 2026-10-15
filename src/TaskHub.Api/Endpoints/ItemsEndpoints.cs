using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Domain.Scoring;
using TaskHub.Infrastructure.Queries;

namespace TaskHub.Api.Endpoints;

internal static class ItemsEndpoints
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const int MaxTodayCount = 50;

    public static RouteGroupBuilder MapItemsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/items", SearchAsync)
            .WithName("GetItems")
            .WithSummary("Liste les éléments actifs, filtrés, triés et paginés.")
            .WithDescription(
                "Filtres multi-valués (répéter le paramètre) : source, project, status. " +
                "dueBefore est inclusif. q cherche dans le titre, l'identifiant externe et le projet. " +
                "sort : updatedAt (défaut), score, dueDate, priority, status, title, project, source ; préfixer par '-' pour l'ordre décroissant (défaut : -updatedAt ; '-score' met les plus urgents en tête). " +
                "Chaque élément porte son score de priorité unifié (0–100) et les raisons de ce score.");

        api.MapGet("/items/today", GetTodayAsync)
            .WithName("GetToday")
            .WithSummary("Les éléments ouverts à traiter en premier (score le plus élevé), hors éléments terminés.")
            .WithDescription($"count : nombre d'éléments (défaut : Scoring:TodayCount, max {MaxTodayCount}).");
        api.MapGet("/items/facets", async (WorkItemQueryService queries, CancellationToken ct) =>
                TypedResults.Ok(WorkItemFacetsDto.From(await queries.GetFacetsAsync(ct))))
            .WithName("GetItemFacets")
            .WithSummary("Valeurs distinctes (sources, projets, statuts) des éléments actifs, avec leur nombre, pour les filtres.");

        return api;
    }

    private static async Task<Results<Ok<WorkItemPageDto>, ValidationProblem>> SearchAsync(
        [AsParameters] ItemsQuery query,
        WorkItemQueryService queries,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var statuses = new List<WorkItemStatus>();
        foreach (var raw in query.Status ?? [])
        {
            if (Enum.TryParse<WorkItemStatus>(raw, ignoreCase: true, out var status) && Enum.IsDefined(status))
            {
                statuses.Add(status);
            }
            else
            {
                errors["status"] = [$"Statut inconnu « {raw} ». Valeurs possibles : {string.Join(", ", Enum.GetNames<WorkItemStatus>())}."];
            }
        }

        var sort = ParseSort(query.Sort);
        if (sort is null)
        {
            errors["sort"] = [$"Tri inconnu « {query.Sort} ». Valeurs possibles : {string.Join(", ", Enum.GetNames<WorkItemSortField>().Select(ToCamelCase))}, avec '-' en préfixe pour l'ordre décroissant."];
        }

        var page = query.Page ?? 1;
        if (page < 1)
        {
            errors["page"] = ["La page doit être supérieure ou égale à 1."];
        }

        var pageSize = query.PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"La taille de page doit être comprise entre 1 et {MaxPageSize}."];
        }

        if (errors.Count > 0 || sort is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var filter = new WorkItemFilter(
            Clean(query.Source),
            Clean(query.Project),
            statuses,
            query.DueBefore,
            query.Q);

        var result = await queries.SearchAsync(filter, sort, page, pageSize, ct);
        return TypedResults.Ok(WorkItemPageDto.From(result));
    }

    private static async Task<Results<Ok<TodayDto>, ValidationProblem>> GetTodayAsync(
        [FromQuery(Name = "count")] int? count,
        WorkItemQueryService queries,
        IOptions<ScoringOptions> scoring,
        CancellationToken ct)
    {
        var take = count ?? scoring.Value.TodayCount;
        if (take is < 1 or > MaxTodayCount)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["count"] = [$"Le nombre d'éléments doit être compris entre 1 et {MaxTodayCount}."],
            });
        }

        var items = await queries.GetTodayAsync(take, ct);
        return TypedResults.Ok(new TodayDto(queries.Today, items.Select(WorkItemDto.From).ToList()));
    }

    internal static WorkItemSort? ParseSort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return WorkItemSort.Default;
        }

        var value = raw.Trim();
        var descending = value.StartsWith('-');
        var name = descending ? value[1..] : value;
        return Enum.TryParse<WorkItemSortField>(name, ignoreCase: true, out var field) && Enum.IsDefined(field) && !int.TryParse(name, out _)
            ? new WorkItemSort(field, descending)
            : null;
    }

    private static List<string> Clean(string[]? values) =>
        values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.Ordinal).ToList() ?? [];

    private static string ToCamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    internal sealed record ItemsQuery(
        [property: FromQuery(Name = "source")] string[]? Source,
        [property: FromQuery(Name = "project")] string[]? Project,
        [property: FromQuery(Name = "status")] string[]? Status,
        [property: FromQuery(Name = "dueBefore")] DateOnly? DueBefore,
        [property: FromQuery(Name = "q")] string? Q,
        [property: FromQuery(Name = "sort")] string? Sort,
        [property: FromQuery(Name = "page")] int? Page,
        [property: FromQuery(Name = "pageSize")] int? PageSize);
}
