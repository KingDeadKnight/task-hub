namespace TaskHub.Infrastructure.Persistence;

/// <summary>One synchronization attempt of one source.</summary>
public sealed class SyncRunEntity
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Succeeded { get; set; }
    public int ItemCount { get; set; }
    public string? ErrorMessage { get; set; }
}
