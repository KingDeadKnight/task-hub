using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources.Jira;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

/// <summary>
/// The real JiraSource behind the real API (only HTTP is stubbed): Jira issues flow through sync, storage
/// and the read API without any Jira-specific code outside the connector.
/// </summary>
public sealed class JiraEndToEndTests
{
    private static HttpResponseMessage JiraPages(RecordedRequest request) =>
        JsonDocument.Parse(request.Body).RootElement.TryGetProperty("nextPageToken", out _)
            ? StubHttpMessageHandler.Json(Fixture.Read("Jira", "search-page-2.json"))
            : StubHttpMessageHandler.Json(Fixture.Read("Jira", "search-page-1.json"));

    [Fact]
    public async Task Jira_issues_are_synced_and_served_like_any_other_source()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var root = new TaskHubApiFactory();
        await using var factory = root.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.PostConfigure<JiraOptions>(o =>
            {
                o.BaseUrl = new Uri("https://contoso.atlassian.net");
                o.Email = "me@example.test";
                o.ApiToken = "test-api-token";
            });
            s.AddHttpClient<JiraSource>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(JiraPages));
        }));
        using var client = factory.CreateClient();

        var sync = await client.PostAsync(new Uri("/api/sync", UriKind.Relative), null, ct);

        sync.StatusCode.Should().Be(HttpStatusCode.OK);
        var sources = await client.GetJsonAsync<SourcesStatusDto>("/api/sources");
        sources.Sources.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "jira", ActiveItemCount = 3 });

        var items = await client.GetJsonAsync<WorkItemPageDto>("/api/items?sort=priority");
        items.Items.Select(i => (i.Id, i.Status, i.NativePriority)).Should().Equal(
            ("jira:OPS-7", WorkItemStatus.Blocked, 1),
            ("jira:MOB-12", WorkItemStatus.InProgress, 2),
            ("jira:MOB-30", WorkItemStatus.Todo, 10100));
        items.Items[1].Url.Should().Be("https://contoso.atlassian.net/browse/MOB-12");

        var facets = await client.GetJsonAsync<WorkItemFacetsDto>("/api/items/facets");
        facets.Projects.Select(p => p.Value).Should().Equal("Mobile App", "Operations");
    }
}
