using Microsoft.EntityFrameworkCore;
using TaskHub.Infrastructure.Health;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Sync;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Health;

public sealed class HealthServiceTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero));
    private readonly SyncOptions _options = new() { Enabled = true, IntervalMinutes = 5 };
    private readonly TestDbContextFactory _dbFactory;
    private readonly HealthService _health;

    public HealthServiceTests()
    {
        _dbFactory = new TestDbContextFactory(_database.ConnectionString);
        _health = new HealthService(_dbFactory, new StaticOptionsMonitor<SyncOptions>(_options), _time);
    }

    public async ValueTask InitializeAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task AddRunAsync(bool succeeded, TimeSpan age)
    {
        await using var db = _dbFactory.CreateDbContext();
        var at = _time.Now - age;
        db.SyncRuns.Add(new SyncRunEntity { Source = "alpha", StartedAt = at, FinishedAt = at, Succeeded = succeeded });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<HealthReport> CheckAsync(bool hasConfiguredSources = true) =>
        _health.CheckAsync(hasConfiguredSources, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Recent_successful_sync_is_healthy()
    {
        await AddRunAsync(succeeded: true, TimeSpan.FromMinutes(14));

        var report = await CheckAsync();

        report.State.Should().Be(HealthState.Healthy);
        report.DatabaseReachable.Should().BeTrue();
        report.LastSuccessfulSyncAt.Should().Be(_time.Now.AddMinutes(-14));
    }

    [Fact]
    public async Task Sync_older_than_three_intervals_is_stale_and_unhealthy()
    {
        await AddRunAsync(succeeded: true, TimeSpan.FromMinutes(16));

        var report = await CheckAsync();

        report.State.Should().Be(HealthState.Unhealthy);
        report.SyncStale.Should().BeTrue();
        report.DatabaseReachable.Should().BeTrue();
    }

    [Fact]
    public async Task Failed_runs_do_not_count_as_a_successful_sync()
    {
        await AddRunAsync(succeeded: true, TimeSpan.FromMinutes(30));
        await AddRunAsync(succeeded: false, TimeSpan.FromMinutes(1));

        var report = await CheckAsync();

        report.SyncStale.Should().BeTrue();
        report.LastSuccessfulSyncAt.Should().Be(_time.Now.AddMinutes(-30));
    }

    [Fact]
    public async Task No_sync_yet_is_healthy_during_startup_then_stale()
    {
        (await CheckAsync()).State.Should().Be(HealthState.Healthy);

        _time.Advance(TimeSpan.FromMinutes(16));

        var report = await CheckAsync();
        report.State.Should().Be(HealthState.Unhealthy);
        report.LastSuccessfulSyncAt.Should().BeNull();
    }

    [Fact]
    public async Task Staleness_is_ignored_without_configured_source_or_with_sync_disabled()
    {
        _time.Advance(TimeSpan.FromDays(1));
        (await CheckAsync(hasConfiguredSources: false)).State.Should().Be(HealthState.Healthy);

        _options.Enabled = false;
        (await CheckAsync()).State.Should().Be(HealthState.Healthy);
    }

    [Fact]
    public async Task Unreachable_database_is_unhealthy()
    {
        await using (var db = _dbFactory.CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }

        var broken = new HealthService(
            new TestDbContextFactory("Data Source=/nonexistent-dir/taskhub.db;Mode=ReadOnly"),
            new StaticOptionsMonitor<SyncOptions>(_options),
            _time);

        var report = await broken.CheckAsync(true, TestContext.Current.CancellationToken);

        report.State.Should().Be(HealthState.Unhealthy);
        report.DatabaseReachable.Should().BeFalse();
    }

    private sealed class TestDbContextFactory(string connectionString) : IDbContextFactory<TaskHubDbContext>
    {
        public TaskHubDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<TaskHubDbContext>().UseSqlite(connectionString).Options);
    }
}
