namespace TaskHub.Domain;

/// <summary>A connector to an external tool that lists the work items assigned to the current user.</summary>
public interface ITaskSource
{
    string Name { get; }

    Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct);
}
