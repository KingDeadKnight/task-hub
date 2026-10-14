namespace TaskHub.Domain.Scoring;

/// <summary>Weights and thresholds of the unified priority score. Weights are relative: the score is scaled to 0–100.</summary>
public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";

    public double DueDateWeight { get; set; } = 40;
    public double PriorityWeight { get; set; } = 30;
    public double StatusWeight { get; set; } = 20;
    public double StalenessWeight { get; set; } = 10;

    /// <summary>Days ahead from which an upcoming due date starts to count.</summary>
    public int DueSoonDays { get; set; } = 14;

    /// <summary>Days without update after which an item counts as fully stale.</summary>
    public int StaleAfterDays { get; set; } = 30;

    /// <summary>
    /// Lowest native priority per source (1 is always the highest). Azure DevOps uses 1–4, Jira 1–5.
    /// Sources not listed use <see cref="DefaultPriorityScale"/>.
    /// </summary>
    public IDictionary<string, int> PriorityScales { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["azuredevops"] = 4,
        ["jira"] = 5,
    };

    public int DefaultPriorityScale { get; set; } = 4;

    /// <summary>Number of items in the "Today" view.</summary>
    public int TodayCount { get; set; } = 5;
}
