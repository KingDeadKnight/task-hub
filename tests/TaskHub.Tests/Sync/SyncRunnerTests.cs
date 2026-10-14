using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Domain;
using TaskHub.Infrastructure;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Sync;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sync;

public sealed class SyncRunnerTests : IAsyncLifetime
{
    private const int RetentionDays = 7;

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeTaskSource _alpha = new("alpha");
    private readonly FakeTaskSource _beta = new("beta");
    private readonly ServiceProvider _services;

    public SyncRunnerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddDbContextFactory<TaskHubDbContext>(o => o.UseSqlite(_database.ConnectionString));
        services.AddOptions<SyncOptions>().Configure(o => o.InactiveRetentionDays = RetentionDays);
        services.AddSingleton<ITaskSource>(_alpha);
        services.AddSingleton<ITaskSource>(_beta);
        services.AddSingleton<SyncRunner>();
        _services = services.BuildServiceProvider();
    }

    private SyncRunner Runner => _services.GetRequiredService<SyncRunner>();

    public async ValueTask InitializeAsync() => await _services.InitializeTaskHubDatabaseAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _database.Dispose();
    }

    private async Task<List<WorkItemEntity>> ItemsAsync()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>().CreateDbContextAsync();
        return await db.WorkItems.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
    }

    private async Task<List<SyncRunEntity>> RunsAsync()
    {
        await using var db = await _services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>().CreateDbContextAsync();
        return await db.SyncRuns.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
    }

    private Task<IReadOnlyList<SyncResult>> SyncAsync() => Runner.RunAllAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task First_sync_inserts_items_and_records_successful_runs()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1"), FakeTaskSource.Item("alpha", "2")];
        _beta.Items = [FakeTaskSource.Item("beta", "1")];

        var results = await SyncAsync();

        results.Should().OnlyContain(r => r.Succeeded);
        (await ItemsAsync()).Select(i => i.Id).Should().Equal("alpha:1", "alpha:2", "beta:1");
        var runs = await RunsAsync();
        runs.Should().HaveCount(2).And.OnlyContain(r => r.Succeeded && r.FinishedAt == _time.Now && r.ErrorMessage == null);
        runs.Single(r => r.Source == "alpha").ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task Next_sync_updates_existing_items()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1", "Old title")];
        await SyncAsync();

        _alpha.Items = [FakeTaskSource.Item("alpha", "1", "New title", WorkItemStatus.InProgress)];
        await SyncAsync();

        var item = (await ItemsAsync()).Single();
        item.Title.Should().Be("New title");
        item.Status.Should().Be(WorkItemStatus.InProgress);
    }

    [Fact]
    public async Task Item_gone_from_source_is_marked_inactive_then_reactivated_when_back()
    {
        var item = FakeTaskSource.Item("alpha", "1");
        _alpha.Items = [item];
        await SyncAsync();

        _alpha.Items = [];
        _time.Advance(TimeSpan.FromHours(1));
        await SyncAsync();

        var inactive = (await ItemsAsync()).Single();
        inactive.IsActive.Should().BeFalse();
        inactive.InactiveSince.Should().Be(_time.Now);

        _alpha.Items = [item];
        await SyncAsync();

        var reactivated = (await ItemsAsync()).Single();
        reactivated.IsActive.Should().BeTrue();
        reactivated.InactiveSince.Should().BeNull();
    }

    [Fact]
    public async Task Inactive_item_is_purged_only_after_retention_period()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1")];
        await SyncAsync();
        _alpha.Items = [];
        await SyncAsync();

        _time.Advance(TimeSpan.FromDays(RetentionDays) - TimeSpan.FromMinutes(1));
        await SyncAsync();
        (await ItemsAsync()).Should().ContainSingle();

        _time.Advance(TimeSpan.FromMinutes(2));
        await SyncAsync();
        (await ItemsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Failing_source_is_recorded_and_does_not_affect_other_sources()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1")];
        _beta.Items = [FakeTaskSource.Item("beta", "1")];
        await SyncAsync();

        _alpha.Items = [FakeTaskSource.Item("alpha", "1"), FakeTaskSource.Item("alpha", "2")];
        _beta.Error = new HttpRequestException("Connection refused");
        var results = await SyncAsync();

        results.Single(r => r.Source == "alpha").Succeeded.Should().BeTrue();
        results.Single(r => r.Source == "beta").Should().BeEquivalentTo(new SyncResult("beta", false, 0, "Connection refused"));

        var lastBetaRun = (await RunsAsync()).Last(r => r.Source == "beta");
        lastBetaRun.Succeeded.Should().BeFalse();
        lastBetaRun.ErrorMessage.Should().Be("Connection refused");
        lastBetaRun.FinishedAt.Should().NotBeNull();

        // A failed fetch must not deactivate the items already stored for that source.
        var items = await ItemsAsync();
        items.Select(i => i.Id).Should().Equal("alpha:1", "alpha:2", "beta:1");
        items.Should().OnlyContain(i => i.IsActive);
    }

    [Fact]
    public async Task Unconfigured_source_is_skipped_without_recording_a_run()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1")];
        _beta.IsConfigured = false;
        _beta.Error = new InvalidOperationException("must not be called");

        var results = await SyncAsync();

        results.Select(r => r.Source).Should().Equal("alpha");
        (await RunsAsync()).Should().OnlyContain(r => r.Source == "alpha");
    }

    [Fact]
    public async Task Source_returning_items_of_another_source_fails_without_writing()
    {
        _alpha.Items = [FakeTaskSource.Item("beta", "1")];

        var results = await SyncAsync();

        results.Single(r => r.Source == "alpha").Succeeded.Should().BeFalse();
        (await ItemsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Concurrent_runs_are_serialized_and_all_recorded()
    {
        _alpha.Items = [FakeTaskSource.Item("alpha", "1")];

        var results = await Task.WhenAll(SyncAsync(), SyncAsync(), SyncAsync());

        results.SelectMany(r => r).Should().HaveCount(6).And.OnlyContain(r => r.Succeeded);
        (await RunsAsync()).Should().HaveCount(6);
        (await ItemsAsync()).Should().ContainSingle();
    }
}
