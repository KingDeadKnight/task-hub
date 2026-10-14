using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TaskHub.Infrastructure.Sources;
using TaskHub.Infrastructure.Sources.Jira;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sources.Jira;

public sealed class JiraSourceTests
{
    private const string Email = "me@example.test";
    private const string Token = "test-api-token";

    private static JiraOptions Configured(params string[] projects)
    {
        var options = new JiraOptions { BaseUrl = "https://contoso.atlassian.net", Email = Email, ApiToken = Token };
        foreach (var project in projects)
        {
            options.Projects.Add(project);
        }

        return options;
    }

    private static JiraSource CreateSource(StubHttpMessageHandler handler, JiraOptions options) =>
        new(
            new HttpClient(handler),
            new StaticOptionsMonitor<JiraOptions>(options),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero)),
            NullLogger<JiraSource>.Instance);

    private static JsonElement Body(RecordedRequest request) => JsonDocument.Parse(request.Body).RootElement;

    /// <summary>Serves page 1 then, when asked with its token, page 2.</summary>
    private static HttpResponseMessage TwoPages(RecordedRequest request) =>
        Body(request).TryGetProperty("nextPageToken", out var token) && token.GetString() == "page-2-token"
            ? StubHttpMessageHandler.Json(Fixture.Read("Jira", "search-page-2.json"))
            : StubHttpMessageHandler.Json(Fixture.Read("Jira", "search-page-1.json"));

    [Fact]
    public async Task Follows_next_page_token_until_last_page()
    {
        var handler = new StubHttpMessageHandler(TwoPages);
        var source = CreateSource(handler, Configured());

        var items = await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        items.Select(i => i.Id).Should().Equal("jira:MOB-12", "jira:OPS-7", "jira:MOB-30");
        handler.Requests.Should().HaveCount(2);
        Body(handler.Requests[0]).TryGetProperty("nextPageToken", out _).Should().BeFalse("the first page has no token");
        Body(handler.Requests[1]).GetProperty("nextPageToken").GetString().Should().Be("page-2-token");
    }

    [Fact]
    public async Task Posts_enhanced_search_with_bounded_jql_and_needed_fields_only()
    {
        var handler = new StubHttpMessageHandler(TwoPages);
        var source = CreateSource(handler, Configured("MOB", "Opé \"rations\""));

        await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri("https://contoso.atlassian.net/rest/api/3/search/jql"));
        var body = Body(request);
        body.GetProperty("jql").GetString().Should().Be(
            "assignee = currentUser() AND resolution = Unresolved AND project IN (\"MOB\", \"Opé \\\"rations\\\"\") ORDER BY updated DESC");
        body.GetProperty("fields").EnumerateArray().Select(f => f.GetString())
            .Should().Equal("summary", "status", "project", "priority", "duedate", "updated");
        body.GetProperty("maxResults").GetInt32().Should().Be(JiraSource.PageSize);
    }

    [Fact]
    public async Task Authenticates_with_email_and_api_token()
    {
        var handler = new StubHttpMessageHandler(TwoPages);
        var source = CreateSource(handler, Configured());

        await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Token}"));
        handler.Requests.Should().OnlyContain(r => r.Authorization == expected);
    }

    [Fact]
    public async Task Stops_at_max_items_and_asks_only_for_what_is_missing()
    {
        var handler = new StubHttpMessageHandler(TwoPages);
        var options = Configured();
        options.MaxItems = 2;
        var source = CreateSource(handler, options);

        var items = await source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        handler.Requests.Should().ContainSingle();
        Body(handler.Requests[0]).GetProperty("maxResults").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Repeated_page_token_stops_with_an_error_instead_of_looping()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(Fixture.Read("Jira", "search-page-1.json")));
        var source = CreateSource(handler, Configured());

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SourceRequestException>().WithMessage("*same page token*");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Bad_credentials_are_reported_without_leaking_them()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var source = CreateSource(handler, Configured());

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<SourceRequestException>())
            .WithMessage("*authentication failed (HTTP 401)*")
            .Which.Message.Should().NotContain(Token).And.NotContain(Email);
    }

    [Fact]
    public async Task Jql_errors_are_reported_with_jira_message()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            """{"errorMessages":["The value 'NOPE' does not exist for the field 'project'."],"errors":{}}""", HttpStatusCode.BadRequest));
        var source = CreateSource(handler, Configured("NOPE"));

        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SourceRequestException>()
            .WithMessage("Jira search failed with HTTP 400: The value 'NOPE' does not exist for the field 'project'..");
    }

    [Fact]
    public async Task Missing_configuration_throws_without_calling_jira()
    {
        var handler = new StubHttpMessageHandler(TwoPages);
        var source = CreateSource(handler, new JiraOptions { BaseUrl = "https://contoso.atlassian.net", Email = Email });

        source.IsConfigured.Should().BeFalse();
        var act = () => source.FetchAssignedToMeAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SourceNotConfiguredException>();
        handler.Requests.Should().BeEmpty();
    }
}
