using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.AzureDevOps;

/// <summary>Lists the open Azure DevOps work items assigned to the PAT owner (WIQL query, then batch fetch).</summary>
public sealed partial class AzureDevOpsSource(
    HttpClient httpClient,
    IOptionsMonitor<AzureDevOpsOptions> options,
    TimeProvider timeProvider,
    ILogger<AzureDevOpsSource> logger) : ITaskSource
{
    /// <summary>Maximum ids accepted by the workitemsbatch endpoint.</summary>
    public const int BatchSize = 200;
    private const string ApiVersion = "7.1";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    public string Name => AzureDevOpsMapper.SourceName;

    public async Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct)
    {
        var settings = options.CurrentValue;
        if (!settings.IsConfigured)
        {
            throw new SourceNotConfiguredException(
                $"Azure DevOps is not configured: set {AzureDevOpsOptions.SectionName}:Organization and {AzureDevOpsOptions.SectionName}:Pat.");
        }

        var stopwatch = Stopwatch.StartNew();
        var orgRoot = new Uri(settings.BaseUrl, Uri.EscapeDataString(settings.Organization) + "/");

        var ids = await QueryAssignedIdsAsync(orgRoot, settings, ct);
        var statusMapper = new StatusMapper(settings.StatusMappings.AsReadOnly());
        var syncedAt = timeProvider.GetUtcNow();
        var items = new List<WorkItem>(ids.Count);

        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var batch = await FetchBatchAsync(orgRoot, settings, chunk, ct);
            items.AddRange(batch.Select(i => AzureDevOpsMapper.Map(i, settings.BaseUrl, settings.Organization, statusMapper, syncedAt)));
        }

        LogFetched(logger, items.Count, stopwatch.ElapsedMilliseconds);
        return items;
    }

    internal static string BuildWiql(IEnumerable<string> projects)
    {
        var query = new StringBuilder(
            "SELECT [System.Id] FROM WorkItems " +
            "WHERE [System.AssignedTo] = @Me " +
            "AND [System.State] NOT IN ('Closed', 'Done', 'Removed')");

        var projectList = projects
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => "'" + p.Trim().Replace("'", "''", StringComparison.Ordinal) + "'")
            .ToList();
        if (projectList.Count > 0)
        {
            query.Append(" AND [System.TeamProject] IN (").AppendJoin(", ", projectList).Append(')');
        }

        query.Append(" ORDER BY [System.ChangedDate] DESC");
        return query.ToString();
    }

    private async Task<IReadOnlyList<int>> QueryAssignedIdsAsync(Uri orgRoot, AzureDevOpsOptions settings, CancellationToken ct)
    {
        var uri = new Uri(orgRoot, $"_apis/wit/wiql?$top={settings.MaxItems}&api-version={ApiVersion}");
        var result = await SendAsync<WiqlResponse>(uri, new WiqlRequest(BuildWiql(settings.Projects)), settings, ct);
        return result?.WorkItems?.Select(w => w.Id).ToList() ?? [];
    }

    private async Task<IReadOnlyList<AdoWorkItem>> FetchBatchAsync(Uri orgRoot, AzureDevOpsOptions settings, int[] ids, CancellationToken ct)
    {
        var uri = new Uri(orgRoot, $"_apis/wit/workitemsbatch?api-version={ApiVersion}");
        var body = new WorkItemsBatchRequest(ids, AzureDevOpsFields.All, ErrorPolicy: "omit");
        var result = await SendAsync<WorkItemsBatchResponse>(uri, body, settings, ct);
        // With errorPolicy "omit", unreadable items come back as null entries.
        return result?.Value?.OfType<AdoWorkItem>().ToList() ?? [];
    }

    private async Task<TResult?> SendAsync<TResult>(Uri uri, object body, AzureDevOpsOptions settings, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body, body.GetType(), options: s_json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", EncodePat(settings.Pat));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);

        // An invalid or expired PAT yields 203 with an HTML sign-in page rather than 401.
        var isJson = response.Content.Headers.ContentType?.MediaType == "application/json";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NonAuthoritativeInformation
            || (response.IsSuccessStatusCode && !isJson))
        {
            throw new SourceRequestException(
                $"Azure DevOps authentication failed (HTTP {(int)response.StatusCode}). Check the PAT and its 'Work Items (Read)' scope.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SourceRequestException($"Azure DevOps request to {uri.AbsolutePath} failed with HTTP {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<TResult>(s_json, ct);
    }

    private static string EncodePat(string pat) => Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + pat));

    [LoggerMessage(Level = LogLevel.Information, Message = "Azure DevOps: fetched {Count} work items in {ElapsedMs} ms")]
    private static partial void LogFetched(ILogger logger, int count, long elapsedMs);
}
