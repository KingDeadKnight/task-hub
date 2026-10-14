namespace TaskHub.Domain;

/// <summary>A work item assigned to the current user, normalized across sources.</summary>
/// <param name="Id">Unique id in the form "{source}:{externalId}".</param>
public sealed record WorkItem(
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
    public static string BuildId(string source, string externalId) => $"{source}:{externalId}";
}
