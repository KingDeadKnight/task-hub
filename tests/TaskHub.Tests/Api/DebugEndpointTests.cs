using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources.AzureDevOps;

namespace TaskHub.Tests.Api;

public sealed class DebugEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Unconfigured_azure_devops_returns_503_problem()
    {
        using var client = factory
            // PostConfigure wins over User Secrets, so a real local PAT never makes this test hit the network.
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.PostConfigure<AzureDevOpsOptions>(o => o.Pat = "")))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/api/debug/azuredevops", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Unknown_source_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/debug/nope", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_items_of_the_named_source_with_string_statuses()
    {
        var item = new WorkItem("fake:1", "fake", "1", "Title", "https://example.test/1", "P", WorkItemStatus.InProgress,
            "Active", 2, new DateOnly(2026, 10, 20), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        using var client = factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITaskSource>(new FakeSource(item))))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/api/debug/fake", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("\"status\":\"InProgress\"").And.Contain("\"id\":\"fake:1\"");
    }

    private sealed class FakeSource(params WorkItem[] items) : ITaskSource
    {
        public string Name => "fake";

        public Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WorkItem>>(items);
    }
}
