using TaskHub.Domain;
using TaskHub.Infrastructure.Queries;
using TaskHub.Infrastructure.Sync;

namespace TaskHub.Api.Contracts;

// Public API contracts: kept separate from domain and EF types so the schema only changes on purpose.

public sealed record WorkItemDto(
    string Id,
    string Source,
    string ExternalId,
    string Title,
    string Url,
    string Project,
    WorkItemStatus Status,
    string RawStatus,
    int? NativePriority,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LastSyncedAt)
{
    public static WorkItemDto From(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(item.Id, item.Source, item.ExternalId, item.Title, item.Url, item.Project, item.Status, item.RawStatus,
            item.NativePriority, item.DueDate, item.UpdatedAt, item.LastSyncedAt);
    }
}

public sealed record WorkItemPageDto(IReadOnlyList<WorkItemDto> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static WorkItemPageDto From(Page<WorkItem> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var totalPages = (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);
        return new(page.Items.Select(WorkItemDto.From).ToList(), page.PageNumber, page.PageSize, page.TotalCount, totalPages);
    }
}

public sealed record SourcesStatusDto(bool IsSyncing, IReadOnlyList<SourceStatusDto> Sources);

public sealed record SourceStatusDto(string Name, int ActiveItemCount, DateTimeOffset? LastSuccessAt, SyncRunDto? LastRun)
{
    public static SourceStatusDto From(SourceSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var run = summary.LastRun;
        return new(summary.Name, summary.ActiveItemCount, summary.LastSuccessAt,
            run is null ? null : new SyncRunDto(run.StartedAt, run.FinishedAt, run.Succeeded, run.ItemCount, run.ErrorMessage));
    }
}

public sealed record SyncRunDto(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Succeeded, int ItemCount, string? Error);

public sealed record SyncResponseDto(IReadOnlyList<SyncSourceResultDto> Results)
{
    public static SyncResponseDto From(IEnumerable<SyncResult> results) =>
        new(results.Select(r => new SyncSourceResultDto(r.Source, r.Succeeded, r.ItemCount, r.Error)).ToList());
}

public sealed record SyncSourceResultDto(string Source, bool Succeeded, int ItemCount, string? Error);
