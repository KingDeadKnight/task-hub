using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskHub.Infrastructure.Sync;

/// <summary>Runs <see cref="SyncRunner"/> at startup, then every <see cref="SyncOptions.IntervalMinutes"/>.</summary>
public sealed partial class SyncService(
    SyncRunner runner,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<SyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await runner.RunAllAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Errors are already isolated per source; this only guards the loop (e.g. database unavailable).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogRunFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Background synchronization is disabled")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Synchronization run failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
