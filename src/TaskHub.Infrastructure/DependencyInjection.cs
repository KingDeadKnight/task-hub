using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskHub.Domain;
using TaskHub.Infrastructure.Persistence;
using TaskHub.Infrastructure.Queries;
using TaskHub.Infrastructure.Sources.AzureDevOps;
using TaskHub.Infrastructure.Sources.Jira;
using TaskHub.Infrastructure.Sync;

namespace TaskHub.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "TaskHub";

    public static IServiceCollection AddTaskHubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        // Resolved lazily so that configuration overrides (tests, environment) are taken into account.
        services.AddDbContextFactory<TaskHubDbContext>((sp, options) =>
            options.UseSqlite(ResolveConnectionString(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHostEnvironment>().ContentRootPath)));

        services.AddOptions<SyncOptions>()
            .Bind(configuration.GetSection(SyncOptions.SectionName))
            .Validate(o => o.IntervalMinutes > 0, "Sync:IntervalMinutes must be positive.")
            .Validate(o => o.InactiveRetentionDays >= 0, "Sync:InactiveRetentionDays must not be negative.")
            .ValidateOnStart();
        services.AddSingleton<SyncRunner>();
        services.AddSingleton<WorkItemQueryService>();
        services.AddHostedService<SyncService>();

        services.AddOptions<AzureDevOpsOptions>().Bind(configuration.GetSection(AzureDevOpsOptions.SectionName));
        services.AddHttpClient<AzureDevOpsSource>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddTransient<ITaskSource>(sp => sp.GetRequiredService<AzureDevOpsSource>());

        services.AddOptions<JiraOptions>().Bind(configuration.GetSection(JiraOptions.SectionName));
        services.AddHttpClient<JiraSource>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddTransient<ITaskSource>(sp => sp.GetRequiredService<JiraSource>());

        return services;
    }

    /// <summary>Applies pending EF Core migrations.</summary>
    public static async Task InitializeTaskHubDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<TaskHubDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>Makes a relative SQLite file path relative to the content root and ensures its folder exists.</summary>
    internal static string ResolveConnectionString(IConfiguration configuration, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(
            configuration.GetConnectionString(ConnectionStringName) ?? "Data Source=taskhub.db");

        var isFile = builder.Mode != SqliteOpenMode.Memory
            && !string.IsNullOrEmpty(builder.DataSource)
            && builder.DataSource != ":memory:";
        if (isFile)
        {
            if (!Path.IsPathRooted(builder.DataSource))
            {
                builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
            }

            var directory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return builder.ToString();
    }
}
