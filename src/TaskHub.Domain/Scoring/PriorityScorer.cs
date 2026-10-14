namespace TaskHub.Domain.Scoring;

public enum ScoreReasonKind
{
    Overdue,
    DueToday,
    DueSoon,
    HighPriority,
    InProgress,
    Blocked,
    Stale,
}

/// <summary>Why an item scores high. <see cref="Days"/> is set for date-based reasons, <see cref="Priority"/> for HighPriority.</summary>
public sealed record ScoreReason(ScoreReasonKind Kind, int? Days = null, int? Priority = null);

/// <summary>Score from 0 (can wait) to 100 (handle first), with its components (each 0–1) and the reasons behind it.</summary>
public sealed record PriorityScore(int Value, double DueDate, double Priority, double Status, double Staleness, IReadOnlyList<ScoreReason> Reasons);

/// <summary>
/// Unified priority score across sources: a weighted mix of due date, normalized native priority,
/// status and staleness. Pure: "now" and "today" are passed in.
/// </summary>
public sealed class PriorityScorer(ScoringOptions options)
{
    /// <summary>Priority component for an item without native priority: slightly below the middle.</summary>
    public const double UnknownPriority = 0.4;

    public PriorityScore Score(WorkItem item, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        var reasons = new List<ScoreReason>();

        var due = DueDateComponent(item.DueDate, today, reasons);
        var priority = PriorityComponent(item, reasons);
        var status = StatusComponent(item.Status, reasons);
        var staleness = StalenessComponent(item, now, reasons);

        var totalWeight = options.DueDateWeight + options.PriorityWeight + options.StatusWeight + options.StalenessWeight;
        var weighted = (due * options.DueDateWeight)
            + (priority * options.PriorityWeight)
            + (status * options.StatusWeight)
            + (staleness * options.StalenessWeight);
        var value = totalWeight <= 0 ? 0 : (int)Math.Round(100 * weighted / totalWeight, MidpointRounding.AwayFromZero);

        return new PriorityScore(Math.Clamp(value, 0, 100), due, priority, status, staleness, reasons);
    }

    /// <summary>1 when overdue, 0.9 due today, then linearly down to 0 at <see cref="ScoringOptions.DueSoonDays"/>.</summary>
    private double DueDateComponent(DateOnly? dueDate, DateOnly today, List<ScoreReason> reasons)
    {
        if (dueDate is not { } due)
        {
            return 0;
        }

        var days = due.DayNumber - today.DayNumber;
        if (days < 0)
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.Overdue, Days: -days));
            return 1;
        }

        if (days == 0)
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.DueToday, Days: 0));
            return 0.9;
        }

        if (days >= options.DueSoonDays)
        {
            return 0;
        }

        // Only flag what is really close; farther dates still raise the score a little.
        if (days <= 3)
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.DueSoon, Days: days));
        }

        return 0.8 * (1 - ((double)days / options.DueSoonDays));
    }

    /// <summary>1 for the highest native priority, 0 for the lowest of the source's scale.</summary>
    private double PriorityComponent(WorkItem item, List<ScoreReason> reasons)
    {
        if (item.NativePriority is not { } priority)
        {
            return UnknownPriority;
        }

        var lowest = options.PriorityScales.TryGetValue(item.Source, out var scale) ? scale : options.DefaultPriorityScale;
        if (lowest <= 1)
        {
            return UnknownPriority;
        }

        var normalized = 1 - ((double)(Math.Clamp(priority, 1, lowest) - 1) / (lowest - 1));
        if (priority == 1)
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.HighPriority, Priority: priority));
        }

        return normalized;
    }

    private static double StatusComponent(WorkItemStatus status, List<ScoreReason> reasons)
    {
        switch (status)
        {
            case WorkItemStatus.InProgress:
                reasons.Add(new ScoreReason(ScoreReasonKind.InProgress));
                return 1;
            case WorkItemStatus.Blocked:
                // Needs someone to unblock it, but cannot be worked on directly.
                reasons.Add(new ScoreReason(ScoreReasonKind.Blocked));
                return 0.8;
            case WorkItemStatus.Todo:
                return 0.3;
            default:
                return 0;
        }
    }

    /// <summary>0 when just updated, 1 after <see cref="ScoringOptions.StaleAfterDays"/> without update.</summary>
    private double StalenessComponent(WorkItem item, DateTimeOffset now, List<ScoreReason> reasons)
    {
        if (item.Status == WorkItemStatus.Done || options.StaleAfterDays <= 0)
        {
            return 0;
        }

        var days = (int)Math.Floor((now - item.UpdatedAt).TotalDays);
        if (days <= 0)
        {
            return 0;
        }

        if (days >= options.StaleAfterDays / 2)
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.Stale, Days: days));
        }

        return Math.Min(1, (double)days / options.StaleAfterDays);
    }
}
