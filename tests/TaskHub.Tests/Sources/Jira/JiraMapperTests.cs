using System.Text.Json;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources.Jira;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sources.Jira;

public sealed class JiraMapperTests
{
    private static readonly Uri s_baseUrl = new("https://contoso.atlassian.net/");
    private static readonly DateTimeOffset s_syncedAt = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly JiraOptions s_options = new();

    private static JiraIssue Load(string fixture, string key)
    {
        using var doc = JsonDocument.Parse(Fixture.Read("Jira", fixture));
        var element = doc.RootElement.GetProperty("issues").EnumerateArray().Single(i => i.GetProperty("key").GetString() == key);
        return element.Deserialize<JiraIssue>(JsonSerializerOptions.Web) ?? throw new InvalidOperationException(key);
    }

    private static WorkItem Map(JiraIssue issue) =>
        JiraMapper.Map(issue, s_baseUrl, new StatusMapper(s_options.StatusMappings.AsReadOnly()), s_options.PriorityMappings.AsReadOnly(), s_syncedAt);

    [Fact]
    public void Maps_all_fields_of_a_complete_issue()
    {
        Map(Load("search-page-1.json", "MOB-12")).Should().BeEquivalentTo(new WorkItem(
            Id: "jira:MOB-12",
            Source: "jira",
            ExternalId: "MOB-12",
            Title: "Crash au démarrage sur Android 15",
            Url: "https://contoso.atlassian.net/browse/MOB-12",
            Project: "Mobile App",
            Status: WorkItemStatus.InProgress,
            RawStatus: "In Review",
            NativePriority: 2,
            DueDate: new DateOnly(2026, 10, 20),
            UpdatedAt: new DateTimeOffset(2026, 10, 14, 10, 15, 30, 123, TimeSpan.FromHours(2)),
            LastSyncedAt: s_syncedAt));
    }

    [Fact]
    public void Configured_status_name_wins_over_status_category()
    {
        var item = Map(Load("search-page-1.json", "OPS-7"));

        item.Status.Should().Be(WorkItemStatus.Blocked);
        item.NativePriority.Should().Be(1);
        item.DueDate.Should().BeNull();
        item.UpdatedAt.Should().Be(new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.FromHours(-5)));
    }

    [Fact]
    public void Unknown_priority_name_falls_back_to_its_id_and_new_category_to_todo()
    {
        var item = Map(Load("search-page-2.json", "MOB-30"));

        item.Status.Should().Be(WorkItemStatus.Todo);
        item.NativePriority.Should().Be(10100);
    }

    [Theory]
    [InlineData("new", WorkItemStatus.Todo)]
    [InlineData("indeterminate", WorkItemStatus.InProgress)]
    [InlineData("done", WorkItemStatus.Done)]
    [InlineData(null, WorkItemStatus.Todo)]
    public void Status_category_gives_the_default_status(string? category, WorkItemStatus expected)
    {
        JiraMapper.FromCategory(category).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-10-14T10:00:00.000+0200", "2026-10-14T08:00:00Z")]
    [InlineData("2026-10-14T10:00:00.000-0130", "2026-10-14T11:30:00Z")]
    [InlineData("2026-10-14T10:00:00.000+02:00", "2026-10-14T08:00:00Z")]
    [InlineData("2026-10-14T10:00:00Z", "2026-10-14T10:00:00Z")]
    public void Parses_jira_timestamps_with_or_without_offset_colon(string raw, string expectedUtc)
    {
        JiraMapper.ParseTimestamp(raw).Should().Be(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Issue_without_fields_uses_key_as_title_and_sync_time()
    {
        var item = Map(new JiraIssue("1", "ABC-1", null));

        item.Title.Should().Be("ABC-1");
        item.UpdatedAt.Should().Be(s_syncedAt);
        item.Project.Should().BeEmpty();
    }
}
