using System.Net;
using System.Net.Http.Json;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class HealthEndpointTests(TaskHubApiFactory factory)
    : IClassFixture<TaskHubApiFactory>
{
    [Fact]
    public async Task Get_health_returns_200_with_ok_status()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(TestContext.Current.CancellationToken);
        body.Should().BeEquivalentTo(new HealthBody("ok"));
    }

    private sealed record HealthBody(string Status);
}
