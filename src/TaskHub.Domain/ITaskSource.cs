namespace TaskHub.Domain;

/// <summary>A connector to an external tool that lists the work items assigned to the current user.</summary>
public interface ITaskSource
{
    string Name { get; }

    /// <summary>
    /// False when the required settings are missing: the source is then left out of synchronization
    /// and status reporting instead of failing on every run.
    /// </summary>
    bool IsConfigured => true;

    Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct);
}
