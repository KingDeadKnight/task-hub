using System.Net;
using System.Text.Json;
using TaskHub.Api.Contracts;
using TaskHub.Domain;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

public sealed class ItemsEndpointTests(SeededApi api) : IClassFixture<SeededApi>
{
    private async Task<WorkItemPageDto> GetAsync(string query = "")
    {
        using var client = api.CreateClient();
        return await client.GetJsonAsync<WorkItemPageDto>("/api/items" + query);
    }

    private async Task<string[]> IdsAsync(string query = "") => (await GetAsync(query)).Items.Select(i => i.Id).ToArray();

    [Fact]
    public async Task Default_lists_active_items_most_recently_updated_first()
    {
        var page = await GetAsync();

        page.Items.Select(i => i.Id).Should().Equal("beta:A-1", "alpha:2", "alpha:1", "alpha:3");
        page.Should().BeEquivalentTo(new { Page = 1, PageSize = 50, TotalCount = 4, TotalPages = 1 });
    }

    [Fact]
    public async Task Returns_dto_with_all_fields()
    {
        var item = (await GetAsync("?q=login")).Items.Single();

        item.Should().BeEquivalentTo(new WorkItemDto(
            "alpha:1", "alpha", "1", "Fix login bug", "https://example.test/1", "Web", WorkItemStatus.InProgress, "InProgress",
            1, new DateOnly(2026, 10, 10), SeededApi.Day(5), SeededApi.Day(14)));
    }

    [Theory]
    [InlineData("?source=beta", new[] { "beta:A-1" })]
    [InlineData("?project=Web&project=Docs", new[] { "alpha:2", "alpha:1", "alpha:3" })]
    [InlineData("?status=blocked&status=InProgress", new[] { "alpha:1", "alpha:3" })]
    [InlineData("?dueBefore=2026-10-15", new[] { "beta:A-1", "alpha:1" })]
    [InlineData("?source=alpha&project=Web&status=Blocked", new[] { "alpha:3" })]
    [InlineData("?project=Nowhere", new string[0])]
    public async Task Filters_are_combined(string query, string[] expected)
    {
        (await IdsAsync(query)).Should().Equal(expected);
    }

    [Theory]
    [InlineData("LOGIN", new[] { "alpha:1" })]
    [InlineData("A-1", new[] { "beta:A-1" })]
    [InlineData("mobile", new[] { "beta:A-1" })]
    [InlineData("100%", new[] { "beta:A-1" })]
    [InlineData("_", new[] { "beta:A-1" })]
    [InlineData("%", new[] { "beta:A-1" })]
    public async Task Text_search_is_case_insensitive_and_treats_wildcards_literally(string q, string[] expected)
    {
        (await IdsAsync("?q=" + Uri.EscapeDataString(q))).Should().Equal(expected);
    }

    [Theory]
    [InlineData("dueDate", new[] { "alpha:1", "beta:A-1", "alpha:3", "alpha:2" })]
    [InlineData("-dueDate", new[] { "alpha:3", "beta:A-1", "alpha:1", "alpha:2" })]
    [InlineData("priority", new[] { "alpha:1", "alpha:3", "alpha:2", "beta:A-1" })]
    [InlineData("status", new[] { "alpha:3", "alpha:1", "alpha:2", "beta:A-1" })]
    [InlineData("title", new[] { "alpha:3", "alpha:1", "beta:A-1", "alpha:2" })]
    [InlineData("updatedAt", new[] { "alpha:3", "alpha:1", "alpha:2", "beta:A-1" })]
    [InlineData("-source", new[] { "beta:A-1", "alpha:1", "alpha:2", "alpha:3" })]
    public async Task Sorts_with_missing_values_last(string sort, string[] expected)
    {
        (await IdsAsync("?sort=" + sort)).Should().Equal(expected);
    }

    [Fact]
    public async Task Paginates_with_totals()
    {
        var page = await GetAsync("?sort=title&page=2&pageSize=3");

        page.Items.Select(i => i.Id).Should().Equal("alpha:2");
        page.Should().BeEquivalentTo(new { Page = 2, PageSize = 3, TotalCount = 4, TotalPages = 2 });
    }

    [Theory]
    [InlineData("?status=Unknown", "status")]
    [InlineData("?sort=colour", "sort")]
    [InlineData("?sort=1", "sort")]
    [InlineData("?page=0", "page")]
    [InlineData("?pageSize=500", "pageSize")]
    public async Task Invalid_parameters_return_validation_problem(string query, string field)
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync(new Uri("/api/items" + query, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
    }
}
