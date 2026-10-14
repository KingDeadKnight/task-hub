using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskHub.Domain;
using TaskHub.Infrastructure.Persistence;

namespace TaskHub.Infrastructure.Sync;

public sealed record SyncResult(string Source, bool Succeeded, int ItemCount, string? Error);

/// <summary>
/// Synchronizes every <see cref="ITaskSource"/> into the database, in parallel, isolating failures per source.
/// Runs never overlap: a call waits for the one in progress.
/// </summary>
public sealed partial class SyncRunner(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<TaskHubDbContext> dbFactory,
    IOptionsMonitor<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<SyncRunner> logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning => _gate.CurrentCount == 0;

    public async Task<IReadOnlyList<SyncResult>> RunAllAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sources = scope.ServiceProvider.GetServices<ITaskSource>().ToList();
            return await Task.WhenAll(sources.Select(s => SyncSourceAsync(s, ct)));
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<SyncResult> SyncSourceAsync(ITaskSource source, CancellationToken ct)
    {
        // The run record has its own context so that a failed data update cannot leave it unsaved.
        await using var runDb = await dbFactory.CreateDbContextAsync(ct);
        var run = new SyncRunEntity { Source = source.Name, StartedAt = timeProvider.GetUtcNow() };
        runDb.SyncRuns.Add(run);
        await runDb.SaveChangesAsync(ct);

        try
        {
            var items = await source.FetchAssignedToMeAsync(ct);
            run.ItemCount = await ApplyAsync(source.Name, items, ct);
            run.Succeeded = true;
            LogSucceeded(logger, source.Name, run.ItemCount);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.ErrorMessage = "Synchronisation annulée.";
            await FinishAsync(runDb, run);
            throw;
        }
#pragma warning disable CA1031 // One failing source must never stop the others.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            run.ErrorMessage = ex.Message;
            LogFailed(logger, source.Name, ex.GetType().Name, ex.Message);
        }

        await FinishAsync(runDb, run);
        return new SyncResult(run.Source, run.Succeeded, run.ItemCount, run.ErrorMessage);
    }

    private async Task FinishAsync(TaskHubDbContext runDb, SyncRunEntity run)
    {
        run.FinishedAt = timeProvider.GetUtcNow();
        // Not cancellable: the outcome of the run must be recorded even during shutdown.
        await runDb.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<int> ApplyAsync(string sourceName, IReadOnlyList<WorkItem> items, CancellationToken ct)
    {
        var foreign = items.FirstOrDefault(i => i.Source != sourceName);
        if (foreign is not null)
        {
            throw new InvalidOperationException($"Source '{sourceName}' returned an item of source '{foreign.Source}'.");
        }

        var now = timeProvider.GetUtcNow();
        var purgeBefore = now.AddDays(-options.CurrentValue.InactiveRetentionDays);
        var fetched = items.DistinctBy(i => i.Id).ToDictionary(i => i.Id);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.WorkItems.Where(w => w.Source == sourceName).ToDictionaryAsync(w => w.Id, ct);

        foreach (var item in fetched.Values)
        {
            if (existing.TryGetValue(item.Id, out var entity))
            {
                entity.UpdateFrom(item);
            }
            else
            {
                db.WorkItems.Add(WorkItemEntity.FromDomain(item));
            }
        }

        foreach (var entity in existing.Values.Where(e => !fetched.ContainsKey(e.Id)))
        {
            if (entity.IsActive)
            {
                entity.IsActive = false;
                entity.InactiveSince = now;
            }
            else if (entity.InactiveSince < purgeBefore)
            {
                db.WorkItems.Remove(entity);
            }
        }

        await db.SaveChangesAsync(ct);
        return fetched.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync of {Source} succeeded: {Count} items")]
    private static partial void LogSucceeded(ILogger logger, string source, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync of {Source} failed ({ErrorType}): {Error}")]
    private static partial void LogFailed(ILogger logger, string source, string errorType, string error);
}
