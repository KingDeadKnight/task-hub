using TaskHub.Domain;
using TaskHub.Domain.Scoring;

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
    Score,
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

/// <summary>Distinct values of the active items, with counts, to populate filters.</summary>
public sealed record WorkItemFacets(
    IReadOnlyList<FacetValue<string>> Sources,
    IReadOnlyList<FacetValue<string>> Projects,
    IReadOnlyList<FacetValue<WorkItemStatus>> Statuses);

public sealed record FacetValue<T>(T Value, int Count);

public sealed record ScoredWorkItem(WorkItem Item, PriorityScore Score);

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record SourceSummary(
    string Name,
    int ActiveItemCount,
    SourceRun? LastRun,
    DateTimeOffset? LastSuccessAt);

public sealed record SourceRun(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Succeeded, int ItemCount, string? ErrorMessage);
