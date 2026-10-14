using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskHub.Domain;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sync;

public sealed class BackgroundSyncTests
{
    [Fact]
    public async Task On_startup_the_database_fills_itself_and_a_broken_source_is_reported_in_sync_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var healthy = new FakeTaskSource("healthy", FakeTaskSource.Item("healthy", "1"), FakeTaskSource.Item("healthy", "2"));
        var broken = new FakeTaskSource("broken") { Error = new InvalidOperationException("Source cassée volontairement") };

        await using var factory = new TaskHubApiFactory();
        await using var app = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(s =>
            {
                s.PostConfigure<Infrastructure.Sync.SyncOptions>(o => o.Enabled = true);
                s.RemoveAll<ITaskSource>();
                s.AddSingleton<ITaskSource>(healthy);
                s.AddSingleton<ITaskSource>(broken);
            });
        });

        // Starting the server runs the hosted SyncService.
        using var client = app.CreateClient();
        var dbFactory = app.Services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>();

        List<SyncRunEntity> runs = [];
        for (var attempt = 0; attempt < 100 && runs.Count(r => r.FinishedAt != null) < 2; attempt++)
        {
            await Task.Delay(100, ct);
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            runs = await db.SyncRuns.AsNoTracking().ToListAsync(ct);
        }

        runs.Should().HaveCount(2);
        runs.Single(r => r.Source == "healthy").Succeeded.Should().BeTrue();
        var brokenRun = runs.Single(r => r.Source == "broken");
        brokenRun.Succeeded.Should().BeFalse();
        brokenRun.ErrorMessage.Should().Be("Source cassée volontairement");

        await using var check = await dbFactory.CreateDbContextAsync(ct);
        (await check.WorkItems.Select(w => w.Id).OrderBy(id => id).ToListAsync(ct)).Should().Equal("healthy:1", "healthy:2");
    }
}
