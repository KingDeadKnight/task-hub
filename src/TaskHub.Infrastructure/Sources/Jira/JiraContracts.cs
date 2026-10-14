namespace TaskHub.Infrastructure.Sources.Jira;

// Wire contracts of POST /rest/api/3/search/jql (Jira Cloud, enhanced search), only the members we read.

internal sealed record JiraSearchRequest(
    string Jql,
    IReadOnlyList<string> Fields,
    int MaxResults,
    string? NextPageToken);

internal sealed record JiraSearchResponse(IReadOnlyList<JiraIssue>? Issues, string? NextPageToken, bool? IsLast);

public sealed record JiraIssue(string Id, string Key, JiraIssueFields? Fields);

public sealed record JiraIssueFields(
    string? Summary,
    JiraStatus? Status,
    JiraProject? Project,
    JiraPriority? Priority,
    string? Duedate,
    string? Updated);

public sealed record JiraStatus(string? Name, JiraStatusCategory? StatusCategory);

/// <summary>Key is one of "new", "indeterminate", "done".</summary>
public sealed record JiraStatusCategory(string? Key, string? Name);

public sealed record JiraProject(string? Key, string? Name);

public sealed record JiraPriority(string? Id, string? Name);
