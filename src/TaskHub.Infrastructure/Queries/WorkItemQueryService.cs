using Microsoft.EntityFrameworkCore;
using TaskHub.Domain;
using TaskHub.Infrastructure.Persistence;

namespace TaskHub.Infrastructure.Queries;

public sealed class WorkItemQueryService(IDbContextFactory<TaskHubDbContext> dbFactory)
{
    public async Task<Page<WorkItem>> SearchAsync(WorkItemFilter filter, WorkItemSort sort, int pageNumber, int pageSize, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = ApplyFilter(db.WorkItems.AsNoTracking().Where(w => w.IsActive), filter);

        var total = await query.CountAsync(ct);
        var entities = await ApplySort(query, sort)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new Page<WorkItem>(entities.Select(e => e.ToDomain()).ToList(), pageNumber, pageSize, total);
    }

    public async Task<WorkItemFacets> GetFacetsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var active = db.WorkItems.AsNoTracking().Where(w => w.IsActive);

        var sources = await active.GroupBy(w => w.Source).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var projects = await active.GroupBy(w => w.Project).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var statuses = await active.GroupBy(w => w.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        return new WorkItemFacets(
            sources.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase).Select(s => new FacetValue<string>(s.Key, s.Count)).ToList(),
            projects.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => new FacetValue<string>(p.Key, p.Count)).ToList(),
            statuses.OrderBy(s => s.Key).Select(s => new FacetValue<WorkItemStatus>(s.Key, s.Count)).ToList());
    }

    public async Task<IReadOnlyList<SourceSummary>> GetSourceSummariesAsync(IEnumerable<string> sourceNames, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var summaries = new List<SourceSummary>();

        foreach (var name in sourceNames.Distinct(StringComparer.Ordinal))
        {
            var runs = db.SyncRuns.AsNoTracking().Where(r => r.Source == name);
            var lastRun = await runs.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
            var lastSuccessAt = await runs.Where(r => r.Succeeded).MaxAsync(r => r.FinishedAt, ct);
            var activeCount = await db.WorkItems.CountAsync(w => w.Source == name && w.IsActive, ct);

            summaries.Add(new SourceSummary(
                name,
                activeCount,
                lastRun is null ? null : new SourceRun(lastRun.StartedAt, lastRun.FinishedAt, lastRun.Succeeded, lastRun.ItemCount, lastRun.ErrorMessage),
                lastSuccessAt));
        }

        return summaries;
    }

    private static IQueryable<WorkItemEntity> ApplyFilter(IQueryable<WorkItemEntity> query, WorkItemFilter filter)
    {
        if (filter.Sources.Count > 0)
        {
            query = query.Where(w => filter.Sources.Contains(w.Source));
        }

        if (filter.Projects.Count > 0)
        {
            query = query.Where(w => filter.Projects.Contains(w.Project));
        }

        if (filter.Statuses.Count > 0)
        {
            query = query.Where(w => filter.Statuses.Contains(w.Status));
        }

        if (filter.DueBefore is { } dueBefore)
        {
            query = query.Where(w => w.DueDate != null && w.DueDate <= dueBefore);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // SQLite LIKE is case-insensitive for ASCII; escape wildcards typed by the user.
            var pattern = "%" + filter.Text.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(w =>
                EF.Functions.Like(w.Title, pattern, "\\")
                || EF.Functions.Like(w.ExternalId, pattern, "\\")
                || EF.Functions.Like(w.Project, pattern, "\\"));
        }

        return query;
    }

    private static IOrderedQueryable<WorkItemEntity> ApplySort(IQueryable<WorkItemEntity> query, WorkItemSort sort)
    {
        var desc = sort.Descending;
        var ordered = sort.Field switch
        {
            // Items without a due date or priority always come last, whatever the direction.
            WorkItemSortField.DueDate => query.OrderBy(w => w.DueDate == null).ThenBy(w => w.DueDate, desc),
            WorkItemSortField.Priority => query.OrderBy(w => w.NativePriority == null).ThenBy(w => w.NativePriority, desc),
            // Workflow order rather than alphabetical: Blocked, InProgress, Todo, Done.
            WorkItemSortField.Status => query.OrderBy(w =>
                w.Status == WorkItemStatus.Blocked ? 0
                : w.Status == WorkItemStatus.InProgress ? 1
                : w.Status == WorkItemStatus.Todo ? 2
                : 3, desc),
            WorkItemSortField.Title => query.OrderBy(w => w.Title, desc),
            WorkItemSortField.Project => query.OrderBy(w => w.Project, desc),
            WorkItemSortField.Source => query.OrderBy(w => w.Source, desc),
            _ => query.OrderBy(w => w.UpdatedAt, desc),
        };

        // Stable order across pages.
        return ordered.ThenBy(w => w.Id);
    }
}

internal static class QueryableOrderingExtensions
{
    public static IOrderedQueryable<T> OrderBy<T, TKey>(this IQueryable<T> source, System.Linq.Expressions.Expression<Func<T, TKey>> key, bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);

    public static IOrderedQueryable<T> ThenBy<T, TKey>(this IOrderedQueryable<T> source, System.Linq.Expressions.Expression<Func<T, TKey>> key, bool descending) =>
        descending ? source.ThenByDescending(key) : source.ThenBy(key);
}
