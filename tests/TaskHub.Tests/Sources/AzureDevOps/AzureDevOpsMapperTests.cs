using System.Text.Json;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources.AzureDevOps;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Sources.AzureDevOps;

public sealed class AzureDevOpsMapperTests
{
    private static readonly Uri s_baseUrl = new("https://dev.azure.com/");
    private static readonly DateTimeOffset s_syncedAt = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly StatusMapper s_statusMapper = new(new AzureDevOpsOptions().StatusMappings.AsReadOnly());

    private static AdoWorkItem LoadItem(int id)
    {
        using var doc = JsonDocument.Parse(Fixture.Read("AzureDevOps", "workitemsbatch-response.json"));
        var element = doc.RootElement.GetProperty("value").EnumerateArray()
            .Single(e => e.ValueKind == JsonValueKind.Object && e.GetProperty("id").GetInt32() == id);
        return element.Deserialize<AdoWorkItem>(JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"Fixture item {id} missing");
    }

    private static WorkItem Map(int id) => AzureDevOpsMapper.Map(LoadItem(id), s_baseUrl, "contoso", s_statusMapper, s_syncedAt);

    [Fact]
    public void Maps_all_fields_of_a_complete_item()
    {
        var item = Map(101);

        item.Should().BeEquivalentTo(new WorkItem(
            Id: "azuredevops:101",
            Source: "azuredevops",
            ExternalId: "101",
            Title: "Fix login redirect loop",
            Url: "https://dev.azure.com/contoso/Fabrikam%20Web/_workitems/edit/101",
            Project: "Fabrikam Web",
            Status: WorkItemStatus.InProgress,
            RawStatus: "Active",
            NativePriority: 1,
            DueDate: new DateOnly(2026, 10, 20),
            UpdatedAt: new DateTimeOffset(2026, 10, 13, 15, 42, 10, 123, TimeSpan.Zero),
            LastSyncedAt: s_syncedAt));
    }

    [Fact]
    public void Blocked_flag_overrides_state_and_target_date_is_used_as_due_date()
    {
        var item = Map(102);

        item.Status.Should().Be(WorkItemStatus.Blocked);
        item.RawStatus.Should().Be("Active");
        item.DueDate.Should().Be(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void Unknown_state_falls_back_to_todo_and_optional_fields_are_null()
    {
        var item = Map(103);

        item.Status.Should().Be(WorkItemStatus.Todo);
        item.RawStatus.Should().Be("Waiting For Review");
        item.NativePriority.Should().BeNull();
        item.DueDate.Should().BeNull();
    }

    [Fact]
    public void Item_without_fields_gets_placeholder_title_and_sync_time()
    {
        var item = AzureDevOpsMapper.Map(new AdoWorkItem(7, null), s_baseUrl, "contoso", s_statusMapper, s_syncedAt);

        item.Title.Should().Be("#7");
        item.UpdatedAt.Should().Be(s_syncedAt);
        item.Url.Should().Be("https://dev.azure.com/contoso/_workitems/edit/7");
    }
}
