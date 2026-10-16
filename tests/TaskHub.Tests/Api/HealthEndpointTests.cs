using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Get_health_returns_200_when_database_answers_and_nothing_is_synchronized()
    {
        await using var factory = new TaskHubApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(TestContext.Current.CancellationToken);
        body.Should().BeEquivalentTo(new HealthBody("ok", true, null, false));
    }

    [Fact]
    public async Task Get_health_reports_the_last_successful_sync()
    {
        await using var factory = new TaskHubApiFactory();
        var finishedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        // The database is created when the host starts, which CreateClient triggers.
        using var client = factory.CreateClient();
        var factoryDb = factory.Services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>();
        await using (var db = await factoryDb.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            db.SyncRuns.Add(new SyncRunEntity { Source = "test", StartedAt = finishedAt, FinishedAt = finishedAt, Succeeded = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var body = await client.GetFromJsonAsync<HealthBody>(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        body.Should().NotBeNull();
        body.LastSyncAt.Should().BeCloseTo(finishedAt, TimeSpan.FromSeconds(1));
    }

    private sealed record HealthBody(string Status, bool Database, DateTimeOffset? LastSyncAt, bool SyncStale);
}
