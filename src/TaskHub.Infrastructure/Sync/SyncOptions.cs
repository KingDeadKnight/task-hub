namespace TaskHub.Infrastructure.Sync;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Runs the periodic background synchronization. Manual runs work regardless.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 5;

    /// <summary>Days an item stays inactive (gone from its source) before being purged.</summary>
    public int InactiveRetentionDays { get; set; } = 7;
}
