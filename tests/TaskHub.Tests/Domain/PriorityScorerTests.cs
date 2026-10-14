using TaskHub.Domain;
using TaskHub.Domain.Scoring;

namespace TaskHub.Tests.Domain;

public sealed class PriorityScorerTests
{
    private static readonly DateOnly s_today = new(2026, 10, 14);
    private static readonly DateTimeOffset s_now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly PriorityScorer _scorer = new(new ScoringOptions());

    private static WorkItem Item(
        string source = "azuredevops",
        WorkItemStatus status = WorkItemStatus.Todo,
        int? priority = null,
        DateOnly? due = null,
        int daysSinceUpdate = 0) =>
        new($"{source}:1", source, "1", "T", "https://example.test", "P", status, status.ToString(), priority, due,
            s_now.AddDays(-daysSinceUpdate), s_now);

    private PriorityScore Score(WorkItem item) => _scorer.Score(item, s_today, s_now);

    [Fact]
    public void Score_is_weighted_mix_scaled_to_100()
    {
        // Overdue (1 × 40) + priority 1 (1 × 30) + in progress (1 × 20) + stale 30 days (1 × 10) = 100.
        Score(Item(status: WorkItemStatus.InProgress, priority: 1, due: s_today.AddDays(-1), daysSinceUpdate: 30)).Value.Should().Be(100);

        // Todo (0.3 × 20) with lowest priority (0 × 30), no due date, just updated = 6.
        Score(Item(priority: 4)).Value.Should().Be(6);
    }

    [Theory]
    [InlineData(-5, 1.0)]
    [InlineData(-1, 1.0)]
    [InlineData(0, 0.9)]
    [InlineData(7, 0.4)]
    [InlineData(14, 0.0)]
    [InlineData(60, 0.0)]
    public void Due_date_component_rises_as_the_date_approaches(int daysFromToday, double expected)
    {
        Score(Item(due: s_today.AddDays(daysFromToday))).DueDate.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void No_due_date_adds_nothing()
    {
        Score(Item()).DueDate.Should().Be(0);
    }

    [Theory]
    [InlineData("azuredevops", 1, 1.0)]
    [InlineData("azuredevops", 2, 2.0 / 3)]
    [InlineData("azuredevops", 4, 0.0)]
    [InlineData("jira", 1, 1.0)]
    [InlineData("jira", 3, 0.5)]
    [InlineData("jira", 5, 0.0)]
    [InlineData("jira", 10100, 0.0)]
    [InlineData("other", 2, 2.0 / 3)]
    public void Native_priority_is_normalized_per_source_scale(string source, int priority, double expected)
    {
        Score(Item(source, priority: priority)).Priority.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Missing_priority_is_slightly_below_neutral()
    {
        Score(Item(priority: null)).Priority.Should().Be(PriorityScorer.UnknownPriority);
    }

    [Theory]
    [InlineData(WorkItemStatus.InProgress, 1.0)]
    [InlineData(WorkItemStatus.Blocked, 0.8)]
    [InlineData(WorkItemStatus.Todo, 0.3)]
    [InlineData(WorkItemStatus.Done, 0.0)]
    public void In_progress_and_blocked_items_weigh_more(WorkItemStatus status, double expected)
    {
        Score(Item(status: status)).Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(15, 0.5)]
    [InlineData(30, 1.0)]
    [InlineData(90, 1.0)]
    public void Staleness_grows_with_days_without_update(int days, double expected)
    {
        Score(Item(daysSinceUpdate: days)).Staleness.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Done_items_are_never_stale()
    {
        Score(Item(status: WorkItemStatus.Done, daysSinceUpdate: 90)).Staleness.Should().Be(0);
    }

    [Fact]
    public void Reasons_explain_the_score()
    {
        var score = Score(Item(status: WorkItemStatus.InProgress, priority: 1, due: s_today.AddDays(-3), daysSinceUpdate: 20));

        score.Reasons.Should().Equal(
            new ScoreReason(ScoreReasonKind.Overdue, Days: 3),
            new ScoreReason(ScoreReasonKind.HighPriority, Priority: 1),
            new ScoreReason(ScoreReasonKind.InProgress),
            new ScoreReason(ScoreReasonKind.Stale, Days: 20));
    }

    [Fact]
    public void Due_soon_and_due_today_are_reported_but_far_dates_are_not()
    {
        Score(Item(due: s_today)).Reasons.Should().ContainSingle().Which.Kind.Should().Be(ScoreReasonKind.DueToday);
        Score(Item(due: s_today.AddDays(2))).Reasons.Should().ContainSingle().Which.Should().Be(new ScoreReason(ScoreReasonKind.DueSoon, Days: 2));
        Score(Item(due: s_today.AddDays(10))).Reasons.Should().BeEmpty();
    }

    [Fact]
    public void Overdue_low_priority_outranks_fresh_high_priority_todo()
    {
        var overdue = Score(Item(priority: 4, due: s_today.AddDays(-1)));
        var important = Score(Item(priority: 1));

        overdue.Value.Should().BeGreaterThan(important.Value);
    }

    [Fact]
    public void Weights_are_configurable()
    {
        var onlyPriority = new PriorityScorer(new ScoringOptions { DueDateWeight = 0, StatusWeight = 0, StalenessWeight = 0, PriorityWeight = 1 });

        onlyPriority.Score(Item(priority: 1, due: s_today.AddDays(-10)), s_today, s_now).Value.Should().Be(100);
        onlyPriority.Score(Item(priority: 4, due: s_today.AddDays(-10)), s_today, s_now).Value.Should().Be(0);
    }
}
