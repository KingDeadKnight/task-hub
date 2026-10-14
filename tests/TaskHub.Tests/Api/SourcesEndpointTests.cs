using TaskHub.Api.Contracts;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class SourcesEndpointTests(SeededApi api) : IClassFixture<SeededApi>
{
    [Fact]
    public async Task Reports_last_run_last_success_and_active_count_per_source()
    {
        using var client = api.CreateClient();

        var status = await client.GetJsonAsync<SourcesStatusDto>("/api/sources");

        status.IsSyncing.Should().BeFalse();
        status.Sources.Should().BeEquivalentTo(new[]
        {
            new SourceStatusDto(
                "alpha",
                ActiveItemCount: 3,
                LastSuccessAt: SeededApi.Day(13).AddSeconds(2),
                LastRun: new SyncRunDto(SeededApi.Day(14), SeededApi.Day(14).AddSeconds(1), Succeeded: false, ItemCount: 0, Error: "HTTP 500")),
            new SourceStatusDto("beta", ActiveItemCount: 1, LastSuccessAt: null, LastRun: null),
        });
    }
}
