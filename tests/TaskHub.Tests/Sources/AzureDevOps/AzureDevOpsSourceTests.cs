using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TaskHub.Infrastructure.Sources;
using TaskHub.Infrastructure.Sources.AzureDevOps;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sources.AzureDevOps;

public sealed class AzureDevOpsSourceTests
{
    private const string Pat = "test-pat-value";

    private static AzureDevOpsOptions ConfiguredOptions(params string[] projects)
    {
        var options = new AzureDevOpsOptions { Organization = "contoso", Pat = Pat };
        foreach (var project in projects)
        {
            options.Projects.Add(project);
        }

        return options;
    }

    private static AzureDevOpsSource CreateSource(StubHttpMessageHandler handler, AzureDevOpsOptions options) =>
        new(
            new HttpClient(handler),
            new StaticOptionsMonitor<AzureDevOpsOptions>(options),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero)),
            NullLogger<AzureDevOpsSource>.Instance);

    private static HttpResponseMessage FixtureResponder(RecordedRequest request) =>
        request.Uri!.AbsolutePath.EndsWith("/wiql", StringComparison.Ordinal)
            ? StubHttpMessageHandler.Json(Fixture.Read("AzureDevOps", "wiql-response.json"))
            : StubHttpMessageHandler.Json(Fixture.Read("AzureDevOps", "workitemsbatch-response.json"));

    [Fact]
    public async Task Fetch_queries_wiql_then_batch_and_maps_items()
    {
        var handler = new StubHttpMessageHandler(FixtureResponder);
        var source = CreateSource(handler, ConfiguredOptions("Fabrikam Web", "Backend"));

        var items = await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        items.Select(i => i.Id).Should().Equal("azuredevops:101", "azuredevops:102", "azuredevops:103");
        handler.Requests.Should().HaveCount(2);

        var wiql = handler.Requests[0];
        wiql.Method.Should().Be(HttpMethod.Post);
        wiql.Uri!.ToString().Should().Be("https://dev.azure.com/contoso/_apis/wit/wiql?$top=1000&api-version=7.1");
        var query = JsonDocument.Parse(wiql.Body).RootElement.GetProperty("query").GetString();
        query.Should().Contain("[System.AssignedTo] = @Me")
            .And.Contain("NOT IN ('Closed', 'Done', 'Removed')")
            .And.Contain("[System.TeamProject] IN ('Fabrikam Web', 'Backend')");

        var batch = handler.Requests[1];
        batch.Uri!.ToString().Should().Be("https://dev.azure.com/contoso/_apis/wit/workitemsbatch?api-version=7.1");
        var batchBody = JsonDocument.Parse(batch.Body).RootElement;
        batchBody.GetProperty("ids").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(101, 102, 103);
        batchBody.GetProperty("errorPolicy").GetString().Should().Be("omit");
        batchBody.GetProperty("fields").EnumerateArray().Select(e => e.GetString()).Should().Contain("System.Title");
    }

    [Fact]
    public async Task Fetch_authenticates_with_basic_pat()
    {
        var handler = new StubHttpMessageHandler(FixtureResponder);
        var source = CreateSource(handler, ConfiguredOptions());

        await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + Pat));
        handler.Requests.Should().OnlyContain(r => r.Authorization == expected);
    }

    [Fact]
    public async Task Fetch_splits_ids_into_batches_of_200()
    {
        var ids = Enumerable.Range(1, 450).ToList();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Uri!.AbsolutePath.EndsWith("/wiql", StringComparison.Ordinal))
            {
                return StubHttpMessageHandler.Json(JsonSerializer.Serialize(new { workItems = ids.Select(id => new { id }) }));
            }

            var requested = JsonDocument.Parse(request.Body).RootElement.GetProperty("ids").EnumerateArray().Select(e => e.GetInt32());
            return StubHttpMessageHandler.Json(JsonSerializer.Serialize(new { value = requested.Select(id => new { id, fields = new { } }) }));
        });
        var source = CreateSource(handler, ConfiguredOptions());

        var items = await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        items.Should().HaveCount(450);
        var batchSizes = handler.Requests.Skip(1)
            .Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("ids").GetArrayLength());
        batchSizes.Should().Equal(200, 200, 50);
    }

    [Fact]
    public async Task Fetch_with_no_assigned_items_skips_batch_call()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""{ "workItems": [] }"""));
        var source = CreateSource(handler, ConfiguredOptions());

        var items = await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        items.Should().BeEmpty();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Invalid_pat_html_sign_in_page_is_reported_as_authentication_failure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation)
        {
            Content = new StringContent("<html>Sign in</html>", Encoding.UTF8, "text/html"),
        });
        var source = CreateSource(handler, ConfiguredOptions());

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<SourceRequestException>())
            .WithMessage("*authentication failed*")
            .Which.Message.Should().NotContain(Pat);
    }

    [Fact]
    public async Task Server_error_is_reported_without_leaking_secrets()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("""{"message":"boom"}""", HttpStatusCode.InternalServerError));
        var source = CreateSource(handler, ConfiguredOptions());

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<SourceRequestException>())
            .WithMessage("*HTTP 500*")
            .Which.Message.Should().NotContain(Pat);
    }

    [Fact]
    public async Task Missing_configuration_throws_without_calling_the_api()
    {
        var handler = new StubHttpMessageHandler(FixtureResponder);
        var source = CreateSource(handler, new AzureDevOpsOptions { Organization = "contoso" });

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SourceNotConfiguredException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void Wiql_escapes_single_quotes_and_omits_project_filter_when_empty()
    {
        AzureDevOpsSource.BuildWiql(["O'Brien"]).Should().Contain("IN ('O''Brien')");
        AzureDevOpsSource.BuildWiql([]).Should().NotContain("System.TeamProject");
    }
}
