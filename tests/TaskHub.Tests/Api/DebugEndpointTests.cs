using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Domain;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class DebugEndpointTests(TaskHubApiFactory factory) : IClassFixture<TaskHubApiFactory>
{
    [Fact]
    public async Task Unconfigured_azure_devops_returns_503_problem()
    {
        using var client = factory.CreateClient();

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
        var item = FakeTaskSource.Item("fake", "1", status: WorkItemStatus.InProgress);
        using var client = factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITaskSource>(new FakeTaskSource("fake", item))))
            .CreateClient();

        var response = await client.GetAsync(new Uri("/api/debug/fake", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("\"status\":\"InProgress\"").And.Contain("\"id\":\"fake:1\"");
    }
}
