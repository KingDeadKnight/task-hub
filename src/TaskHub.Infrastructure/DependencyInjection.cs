using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskHub.Domain;
using TaskHub.Infrastructure.Sources.AzureDevOps;

namespace TaskHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskHubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<AzureDevOpsOptions>().Bind(configuration.GetSection(AzureDevOpsOptions.SectionName));
        services.AddHttpClient<AzureDevOpsSource>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddTransient<ITaskSource>(sp => sp.GetRequiredService<AzureDevOpsSource>());

        return services;
    }
}
