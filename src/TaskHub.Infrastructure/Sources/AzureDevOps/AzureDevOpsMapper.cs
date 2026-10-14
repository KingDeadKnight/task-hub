using System.Globalization;
using System.Text.Json;
using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.AzureDevOps;

/// <summary>Pure mapping from an Azure DevOps work item to a <see cref="WorkItem"/>.</summary>
public static class AzureDevOpsMapper
{
    public const string SourceName = "azuredevops";

    public static WorkItem Map(AdoWorkItem item, Uri baseUrl, string organization, StatusMapper statusMapper, DateTimeOffset syncedAt)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(statusMapper);

        var fields = item.Fields ?? new Dictionary<string, JsonElement>();
        var externalId = item.Id.ToString(CultureInfo.InvariantCulture);
        var project = GetString(fields, AzureDevOpsFields.TeamProject) ?? "";
        var rawStatus = GetString(fields, AzureDevOpsFields.State) ?? "";

        // CMMI exposes a "Blocked" flag independent of the state.
        var status = string.Equals(GetString(fields, AzureDevOpsFields.Blocked), "Yes", StringComparison.OrdinalIgnoreCase)
            ? WorkItemStatus.Blocked
            : statusMapper.Map(rawStatus);

        var dueDate = GetDate(fields, AzureDevOpsFields.DueDate) ?? GetDate(fields, AzureDevOpsFields.TargetDate);

        return new WorkItem(
            Id: WorkItem.BuildId(SourceName, externalId),
            Source: SourceName,
            ExternalId: externalId,
            Title: GetString(fields, AzureDevOpsFields.Title) ?? $"#{externalId}",
            Url: BuildItemUrl(baseUrl, organization, project, item.Id),
            Project: project,
            Status: status,
            RawStatus: rawStatus,
            NativePriority: GetInt(fields, AzureDevOpsFields.Priority),
            DueDate: dueDate is { } d ? DateOnly.FromDateTime(d.UtcDateTime) : null,
            UpdatedAt: GetDate(fields, AzureDevOpsFields.ChangedDate) ?? syncedAt,
            LastSyncedAt: syncedAt);
    }

    public static string BuildItemUrl(Uri baseUrl, string organization, string project, int id)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        var root = baseUrl.AbsoluteUri.TrimEnd('/');
        var org = Uri.EscapeDataString(organization);
        return string.IsNullOrEmpty(project)
            ? $"{root}/{org}/_workitems/edit/{id}"
            : $"{root}/{org}/{Uri.EscapeDataString(project)}/_workitems/edit/{id}";
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetInt(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;

    private static DateTimeOffset? GetDate(IReadOnlyDictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var d) ? d : null;
}
