using System.Text.Json;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class OpenApiTests(TaskHubApiFactory factory) : IClassFixture<TaskHubApiFactory>
{
    [Fact]
    public async Task Document_describes_public_endpoints_only_with_string_enums()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.Should().Contain(["/api/items", "/api/sources", "/api/sync", "/api/health"]);
        paths.Should().NotContain(p => p.StartsWith("/api/debug", StringComparison.Ordinal));

        var status = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("WorkItemStatus");
        status.GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Todo", "InProgress", "Blocked", "Done");
    }
}
