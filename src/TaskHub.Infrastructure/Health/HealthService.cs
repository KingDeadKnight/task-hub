using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Sync;

namespace TaskHub.Infrastructure.Health;

public enum HealthState
{
    Healthy,
    Unhealthy,
}

/// <param name="State">Overall verdict: unhealthy when the database fails or the synchronization is stale.</param>
/// <param name="DatabaseReachable">Whether a query against the database succeeded.</param>
/// <param name="LastSuccessfulSyncAt">Finish time of the most recent successful run of any source, if any.</param>
/// <param name="SyncStale">Whether synchronization is expected but has not succeeded recently enough.</param>
public sealed record HealthReport(HealthState State, bool DatabaseReachable, DateTimeOffset? LastSuccessfulSyncAt, bool SyncStale);

/// <summary>
/// Checks that the database answers and that the synchronization is still producing successful runs.
/// A sync is considered stale after <see cref="StaleAfterIntervals"/> intervals without success.
/// </summary>
public sealed class HealthService(
    IDbContextFactory<TaskHubDbContext> dbFactory,
    IOptionsMonitor<SyncOptions> syncOptions,
    TimeProvider timeProvider)
{
    internal const int StaleAfterIntervals = 3;

    private readonly DateTimeOffset _startedAt = timeProvider.GetUtcNow();

    /// <param name="hasConfiguredSources">Without any configured source nothing is synchronized, so there is nothing to be stale.</param>
    public async Task<HealthReport> CheckAsync(bool hasConfiguredSources, CancellationToken ct)
    {
        DateTimeOffset? lastSync;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            lastSync = await db.SyncRuns
                .Where(r => r.Succeeded && r.FinishedAt != null)
                .MaxAsync(r => r.FinishedAt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthReport(HealthState.Unhealthy, false, null, false);
        }

        var options = syncOptions.CurrentValue;
        var stale = false;
        if (options.Enabled && hasConfiguredSources)
        {
            var maxAge = TimeSpan.FromMinutes(options.IntervalMinutes * StaleAfterIntervals);
            // Before the first success, the clock starts at process start: leave time for the initial sync.
            var reference = lastSync ?? _startedAt;
            stale = timeProvider.GetUtcNow() - reference > maxAge;
        }

        return new HealthReport(stale ? HealthState.Unhealthy : HealthState.Healthy, true, lastSync, stale);
    }
}
