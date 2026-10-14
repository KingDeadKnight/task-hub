using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskHub.Infrastructure.Persistence;

public sealed class TaskHubDbContext(DbContextOptions<TaskHubDbContext> options) : DbContext(options)
{
    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        // SQLite cannot compare or sort DateTimeOffset stored as text: store UTC Unix milliseconds instead.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UnixMillisecondsConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<WorkItemEntity>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasMaxLength(200);
            e.Property(w => w.Source).HasMaxLength(50);
            e.Property(w => w.ExternalId).HasMaxLength(100);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(w => new { w.Source, w.IsActive });
        });

        modelBuilder.Entity<SyncRunEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Source).HasMaxLength(50);
            e.HasIndex(r => new { r.Source, r.StartedAt });
        });
    }

    private sealed class UnixMillisecondsConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.ToUnixTimeMilliseconds(),
        v => DateTimeOffset.FromUnixTimeMilliseconds(v));
}
