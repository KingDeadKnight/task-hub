using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class SyncEndpointTests
{
    private static readonly Uri s_sync = new("/api/sync", UriKind.Relative);

    [Fact]
    public async Task Post_sync_runs_all_sources_and_items_become_readable()
    {
        var ct = TestContext.Current.CancellationToken;
        var ok = new FakeTaskSource("ok", FakeTaskSource.Item("ok", "1"));
        var broken = new FakeTaskSource("broken") { Error = new InvalidOperationException("boom") };
        await using var root = new TaskHubApiFactory();
        await using var factory = root.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITaskSource>();
            s.AddSingleton<ITaskSource>(ok);
            s.AddSingleton<ITaskSource>(broken);
        }));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(s_sync, null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncResponseDto>(ApiJson.Options, ct);
        body.Should().BeEquivalentTo(new SyncResponseDto(
        [
            new SyncSourceResultDto("ok", true, 1, null),
            new SyncSourceResultDto("broken", false, 0, "boom"),
        ]));

        (await client.GetJsonAsync<WorkItemPageDto>("/api/items")).Items.Select(i => i.Id).Should().Equal("ok:1");
        var sources = await client.GetJsonAsync<SourcesStatusDto>("/api/sources");
        sources.Sources.Single(s => s.Name == "broken").LastRun.Should().BeEquivalentTo(new { Succeeded = false, Error = "boom" });
    }

    [Fact]
    public async Task Post_sync_while_a_sync_is_running_returns_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new BlockingSource();
        await using var root = new TaskHubApiFactory();
        await using var factory = root.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITaskSource>();
            s.AddSingleton<ITaskSource>(source);
        }));
        using var client = factory.CreateClient();

        var first = client.PostAsync(s_sync, null, ct);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        var second = await client.PostAsync(s_sync, null, ct);
        (await client.GetJsonAsync<SourcesStatusDto>("/api/sources")).IsSyncing.Should().BeTrue();

        source.Release.SetResult();
        (await first).StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed class BlockingSource : ITaskSource
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "slow";

        public async Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return [];
        }
    }
}
