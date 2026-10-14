using System.Globalization;
using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.Jira;

/// <summary>Pure mapping from a Jira issue to a <see cref="WorkItem"/>.</summary>
public static class JiraMapper
{
    public const string SourceName = "jira";

    public static readonly IReadOnlyList<string> Fields = ["summary", "status", "project", "priority", "duedate", "updated"];

    public static WorkItem Map(
        JiraIssue issue,
        Uri baseUrl,
        StatusMapper statusMapper,
        IReadOnlyDictionary<string, int> priorityMappings,
        DateTimeOffset syncedAt)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(statusMapper);
        ArgumentNullException.ThrowIfNull(priorityMappings);

        var fields = issue.Fields;
        var rawStatus = fields?.Status?.Name ?? "";

        return new WorkItem(
            Id: WorkItem.BuildId(SourceName, issue.Key),
            Source: SourceName,
            ExternalId: issue.Key,
            Title: string.IsNullOrWhiteSpace(fields?.Summary) ? issue.Key : fields.Summary,
            Url: $"{baseUrl.AbsoluteUri.TrimEnd('/')}/browse/{Uri.EscapeDataString(issue.Key)}",
            Project: fields?.Project?.Name ?? fields?.Project?.Key ?? "",
            Status: statusMapper.TryMap(rawStatus, out var mapped) ? mapped : FromCategory(fields?.Status?.StatusCategory?.Key),
            RawStatus: rawStatus,
            NativePriority: MapPriority(fields?.Priority, priorityMappings),
            DueDate: DateOnly.TryParseExact(fields?.Duedate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) ? due : null,
            UpdatedAt: ParseTimestamp(fields?.Updated) ?? syncedAt,
            LastSyncedAt: syncedAt);
    }

    internal static WorkItemStatus FromCategory(string? categoryKey) => categoryKey switch
    {
        "indeterminate" => WorkItemStatus.InProgress,
        "done" => WorkItemStatus.Done,
        _ => WorkItemStatus.Todo,
    };

    private static int? MapPriority(JiraPriority? priority, IReadOnlyDictionary<string, int> mappings)
    {
        if (priority?.Name is { } name && mappings.TryGetValue(name, out var mapped))
        {
            return mapped;
        }

        // Custom priority names: the id of the default scheme follows the same 1 (highest) to 5 (lowest) order.
        return int.TryParse(priority?.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>Jira timestamps look like "2026-10-14T10:00:00.000+0200": the offset has no colon.</summary>
    internal static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length > 5 && text[^5] is '+' or '-' && text[^4..].All(char.IsAsciiDigit))
        {
            text = text[..^2] + ":" + text[^2..];
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }
}
