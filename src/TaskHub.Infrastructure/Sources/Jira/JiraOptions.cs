using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.Jira;

public sealed class JiraOptions
{
    public const string SectionName = "Sources:Jira";

    /// <summary>Jira Cloud site, e.g. https://your-domain.atlassian.net. A string so that an empty value just means "not configured".</summary>
    public string BaseUrl { get; set; } = "";

    public Uri? SiteUri => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri : null;

    /// <summary>Atlassian account e-mail used with the API token.</summary>
    public string Email { get; set; } = "";

    /// <summary>API token (id.atlassian.com, "Security"). Secret: User Secrets or environment only.</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>Project keys or names to include. Empty means every project.</summary>
    public IList<string> Projects { get; } = [];

    /// <summary>Upper bound on the number of issues fetched.</summary>
    public int MaxItems { get; set; } = 1000;

    /// <summary>
    /// Native status name to unified status. Statuses not listed fall back to their Jira status category
    /// (To Do → Todo, In Progress → InProgress, Done → Done).
    /// </summary>
    public IDictionary<string, WorkItemStatus> StatusMappings { get; } =
        new Dictionary<string, WorkItemStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["Blocked"] = WorkItemStatus.Blocked,
            ["On Hold"] = WorkItemStatus.Blocked,
            ["Bloqué"] = WorkItemStatus.Blocked,
        };

    /// <summary>Priority name to native priority number (1 = most important), like Azure DevOps.</summary>
    public IDictionary<string, int> PriorityMappings { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Highest"] = 1,
            ["High"] = 2,
            ["Medium"] = 3,
            ["Low"] = 4,
            ["Lowest"] = 5,
        };

    public bool IsConfigured =>
        SiteUri is not null && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(ApiToken);
}
