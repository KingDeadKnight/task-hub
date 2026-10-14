using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.AzureDevOps;

public sealed class AzureDevOpsOptions
{
    public const string SectionName = "Sources:AzureDevOps";

    public Uri BaseUrl { get; set; } = new("https://dev.azure.com/");

    /// <summary>Organization name, as in https://dev.azure.com/{organization}.</summary>
    public string Organization { get; set; } = "";

    /// <summary>Projects to include. Empty means every project of the organization.</summary>
    public IList<string> Projects { get; } = [];

    /// <summary>Personal access token (scope "Work Items: Read"). Secret: User Secrets or environment only.</summary>
    public string Pat { get; set; } = "";

    /// <summary>Upper bound on the number of items returned by the WIQL query.</summary>
    public int MaxItems { get; set; } = 1000;

    /// <summary>Native state to unified status. Configuration entries are added to (or override) the defaults.</summary>
    public IDictionary<string, WorkItemStatus> StatusMappings { get; } =
        new Dictionary<string, WorkItemStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["New"] = WorkItemStatus.Todo,
            ["To Do"] = WorkItemStatus.Todo,
            ["Proposed"] = WorkItemStatus.Todo,
            ["Approved"] = WorkItemStatus.Todo,
            ["Committed"] = WorkItemStatus.InProgress,
            ["Active"] = WorkItemStatus.InProgress,
            ["In Progress"] = WorkItemStatus.InProgress,
            ["Doing"] = WorkItemStatus.InProgress,
            ["Blocked"] = WorkItemStatus.Blocked,
            ["Resolved"] = WorkItemStatus.Done,
            ["Done"] = WorkItemStatus.Done,
            ["Closed"] = WorkItemStatus.Done,
        };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Organization) && !string.IsNullOrWhiteSpace(Pat);
}
