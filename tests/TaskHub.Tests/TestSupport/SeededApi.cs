using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskHub.Domain;
using TaskHub.Infrastructure.Persistence;

namespace TaskHub.Tests.TestSupport;

/// <summary>
/// API with sources "alpha" and "beta" and a fixed data set (read-only tests share it):
/// <code>
/// id         project  status      prio  due         updated
/// alpha:1    Web      InProgress  1     2026-10-10  2026-10-05
/// alpha:2    Docs     Todo        3     -           2026-10-12
/// alpha:3    Web      Blocked     2     2026-10-20  2026-10-01
/// beta:A-1   Mobile   Todo        -     2026-10-15  2026-10-13
/// beta:A-2   Mobile   Todo        (inactive)
/// </code>
/// "Now" is 2026-10-14 09:00 UTC; scores: alpha:1 = 93, alpha:3 = 59, beta:A-1 = 48, alpha:2 = 17.
/// </summary>
public sealed class SeededApi : IAsyncLifetime
{
    private readonly TaskHubApiFactory _root = new();

    public SeededApi()
    {
        Factory = _root.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            // Scores depend on "now": freeze it at 2026-10-14 09:00 UTC.
            s.AddSingleton<TimeProvider>(new FixedTimeProvider(Day(14)));
            s.RemoveAll<ITaskSource>();
            s.AddSingleton<ITaskSource>(new FakeTaskSource("alpha"));
            s.AddSingleton<ITaskSource>(new FakeTaskSource("beta"));
        }));
    }

    public static DateTimeOffset Day(int day) => new(2026, 10, day, 9, 0, 0, TimeSpan.Zero);

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient CreateClient() => Factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        using var _ = Factory.CreateClient(); // starts the host, which applies migrations
        await using var db = await Factory.Services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>().CreateDbContextAsync();

        db.WorkItems.AddRange(
            Entity("alpha", "1", "Fix login bug", "Web", WorkItemStatus.InProgress, 1, new DateOnly(2026, 10, 10), Day(5)),
            Entity("alpha", "2", "Write docs", "Docs", WorkItemStatus.Todo, 3, null, Day(12)),
            Entity("alpha", "3", "Blocked deploy", "Web", WorkItemStatus.Blocked, 2, new DateOnly(2026, 10, 20), Day(1)),
            Entity("beta", "A-1", "Review 100% coverage_plan", "Mobile", WorkItemStatus.Todo, null, new DateOnly(2026, 10, 15), Day(13)),
            Entity("beta", "A-2", "Old closed item", "Mobile", WorkItemStatus.Todo, null, null, Day(2), isActive: false));

        db.SyncRuns.AddRange(
            new SyncRunEntity { Source = "alpha", StartedAt = Day(13), FinishedAt = Day(13).AddSeconds(2), Succeeded = true, ItemCount = 3 },
            new SyncRunEntity { Source = "alpha", StartedAt = Day(14), FinishedAt = Day(14).AddSeconds(1), Succeeded = false, ErrorMessage = "HTTP 500" });

        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _root.DisposeAsync();
    }

    private static WorkItemEntity Entity(
        string source, string externalId, string title, string project, WorkItemStatus status, int? priority, DateOnly? due,
        DateTimeOffset updatedAt, bool isActive = true) =>
        new()
        {
            Id = WorkItem.BuildId(source, externalId),
            Source = source,
            ExternalId = externalId,
            Title = title,
            Url = $"https://example.test/{externalId}",
            Project = project,
            Status = status,
            RawStatus = status.ToString(),
            NativePriority = priority,
            DueDate = due,
            UpdatedAt = updatedAt,
            LastSyncedAt = Day(14),
            IsActive = isActive,
            InactiveSince = isActive ? null : Day(3),
        };
}
