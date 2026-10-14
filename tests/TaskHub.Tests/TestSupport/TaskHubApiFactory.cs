using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Infrastructure.Sources.AzureDevOps;

namespace TaskHub.Tests.TestSupport;

/// <summary>
/// Runs the API against a throwaway SQLite file, with background sync disabled and no Azure DevOps PAT,
/// so tests never touch the developer database nor the network (even with User Secrets set).
/// </summary>
public sealed class TaskHubApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteTestDatabase _database = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TaskHub"] = _database.ConnectionString,
            ["Sync:Enabled"] = "false",
        }));
        builder.ConfigureTestServices(services => services.PostConfigure<AzureDevOpsOptions>(o => o.Pat = ""));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _database.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _database.Dispose();
        }
    }
}
