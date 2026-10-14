using TaskHub.Domain;

namespace TaskHub.Infrastructure.Persistence;

public sealed class WorkItemEntity
{
    public required string Id { get; set; }
    public required string Source { get; set; }
    public required string ExternalId { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Project { get; set; }
    public WorkItemStatus Status { get; set; }
    public required string RawStatus { get; set; }
    public int? NativePriority { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <summary>False once the item no longer comes back from its source (closed or reassigned).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? InactiveSince { get; set; }

    public static WorkItemEntity FromDomain(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entity = new WorkItemEntity
        {
            Id = item.Id,
            Source = item.Source,
            ExternalId = item.ExternalId,
            Title = item.Title,
            Url = item.Url,
            Project = item.Project,
            RawStatus = item.RawStatus,
        };
        entity.UpdateFrom(item);
        return entity;
    }

    public void UpdateFrom(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Title = item.Title;
        Url = item.Url;
        Project = item.Project;
        Status = item.Status;
        RawStatus = item.RawStatus;
        NativePriority = item.NativePriority;
        DueDate = item.DueDate;
        UpdatedAt = item.UpdatedAt;
        LastSyncedAt = item.LastSyncedAt;
        IsActive = true;
        InactiveSince = null;
    }

    public WorkItem ToDomain() =>
        new(Id, Source, ExternalId, Title, Url, Project, Status, RawStatus, NativePriority, DueDate, UpdatedAt, LastSyncedAt);
}
