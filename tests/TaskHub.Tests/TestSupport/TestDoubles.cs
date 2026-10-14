using Microsoft.Extensions.Options;
using TaskHub.Domain;

namespace TaskHub.Tests.TestSupport;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}

internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>An in-memory source whose items or failure can be changed between syncs.</summary>
internal sealed class FakeTaskSource(string name, params WorkItem[] items) : ITaskSource
{
    public string Name => name;

    public IReadOnlyList<WorkItem> Items { get; set; } = items;

    public Exception? Error { get; set; }

    public Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct) =>
        Error is null ? Task.FromResult(Items) : Task.FromException<IReadOnlyList<WorkItem>>(Error);

    public static WorkItem Item(string source, string externalId, string title = "Title", WorkItemStatus status = WorkItemStatus.Todo) =>
        new(
            WorkItem.BuildId(source, externalId), source, externalId, title, $"https://example.test/{externalId}", "Project",
            status, status.ToString(), 2, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}
