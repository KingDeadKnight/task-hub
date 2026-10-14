using TaskHub.Domain;

namespace TaskHub.Infrastructure.Queries;

/// <summary>Criteria for listing active work items. Empty collections mean "no filter".</summary>
public sealed record WorkItemFilter(
    IReadOnlyCollection<string> Sources,
    IReadOnlyCollection<string> Projects,
    IReadOnlyCollection<WorkItemStatus> Statuses,
    DateOnly? DueBefore,
    string? Text)
{
    public static WorkItemFilter None { get; } = new([], [], [], null, null);
}

public enum WorkItemSortField
{
    UpdatedAt,
    DueDate,
    Priority,
    Status,
    Title,
    Project,
    Source,
}

public sealed record WorkItemSort(WorkItemSortField Field, bool Descending)
{
    public static WorkItemSort Default { get; } = new(WorkItemSortField.UpdatedAt, Descending: true);
}

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record SourceSummary(
    string Name,
    int ActiveItemCount,
    SourceRun? LastRun,
    DateTimeOffset? LastSuccessAt);

public sealed record SourceRun(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Succeeded, int ItemCount, string? ErrorMessage);
