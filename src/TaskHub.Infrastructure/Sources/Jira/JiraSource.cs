using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskHub.Domain;

namespace TaskHub.Infrastructure.Sources.Jira;

/// <summary>
/// Lists the unresolved Jira Cloud issues assigned to the API token owner, through the enhanced search
/// (POST /rest/api/3/search/jql, paginated with nextPageToken).
/// </summary>
public sealed partial class JiraSource(
    HttpClient httpClient,
    IOptionsMonitor<JiraOptions> options,
    TimeProvider timeProvider,
    ILogger<JiraSource> logger) : ITaskSource
{
    public const int PageSize = 100;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => JiraMapper.SourceName;

    public bool IsConfigured => options.CurrentValue.IsConfigured;

    public async Task<IReadOnlyList<WorkItem>> FetchAssignedToMeAsync(CancellationToken ct)
    {
        var settings = options.CurrentValue;
        if (!settings.IsConfigured || settings.SiteUri is not { } site)
        {
            throw new SourceNotConfiguredException(
                $"Jira is not configured: set {JiraOptions.SectionName}:BaseUrl, {JiraOptions.SectionName}:Email and {JiraOptions.SectionName}:ApiToken.");
        }

        var stopwatch = Stopwatch.StartNew();
        var uri = new Uri(site, "/rest/api/3/search/jql");
        var jql = BuildJql(settings.Projects);
        var statusMapper = new StatusMapper(settings.StatusMappings.AsReadOnly());
        var priorities = settings.PriorityMappings.AsReadOnly();
        var syncedAt = timeProvider.GetUtcNow();

        var items = new List<WorkItem>();
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);
        string? nextPageToken = null;
        var pages = 0;

        do
        {
            var pageSize = Math.Min(PageSize, settings.MaxItems - items.Count);
            var page = await SearchAsync(uri, new JiraSearchRequest(jql, JiraMapper.Fields, pageSize, nextPageToken), settings, ct);
            pages++;
            items.AddRange((page?.Issues ?? []).Select(i => JiraMapper.Map(i, site, statusMapper, priorities, syncedAt)));

            nextPageToken = page?.IsLast == true ? null : page?.NextPageToken;
            // Guard against a token that would loop forever.
            if (nextPageToken is not null && !seenTokens.Add(nextPageToken))
            {
                throw new SourceRequestException("Jira returned the same page token twice; pagination stopped.");
            }
        }
        while (nextPageToken is not null && items.Count < settings.MaxItems);

        LogFetched(logger, items.Count, pages, stopwatch.ElapsedMilliseconds);
        return items;
    }

    internal static string BuildJql(IEnumerable<string> projects)
    {
        var query = new StringBuilder("assignee = currentUser() AND resolution = Unresolved");

        var projectList = projects
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => "\"" + p.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"")
            .ToList();
        if (projectList.Count > 0)
        {
            query.Append(" AND project IN (").AppendJoin(", ", projectList).Append(')');
        }

        query.Append(" ORDER BY updated DESC");
        return query.ToString();
    }

    private async Task<JiraSearchResponse?> SearchAsync(Uri uri, JiraSearchRequest body, JiraOptions settings, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body, options: s_json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Email}:{settings.ApiToken}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new SourceRequestException(
                $"Jira authentication failed (HTTP {(int)response.StatusCode}). Check the e-mail and API token.");
        }

        if (!response.IsSuccessStatusCode)
        {
            // Jira error bodies ({"errorMessages": [...]}) describe JQL problems and contain no secrets.
            var detail = await ReadErrorMessagesAsync(response, ct);
            throw new SourceRequestException(
                $"Jira search failed with HTTP {(int)response.StatusCode}{(detail is null ? "" : ": " + detail)}.");
        }

        return await response.Content.ReadFromJsonAsync<JiraSearchResponse>(s_json, ct);
    }

    private static async Task<string?> ReadErrorMessagesAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("errorMessages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                var text = string.Join(" ", messages.EnumerateArray().Select(m => m.GetString()).Where(m => !string.IsNullOrEmpty(m)));
                return text.Length == 0 ? null : text[..Math.Min(text.Length, 300)];
            }
        }
        catch (JsonException)
        {
            // Not a Jira error body (e.g. HTML from a proxy).
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Jira: fetched {Count} issues in {Pages} page(s), {ElapsedMs} ms")]
    private static partial void LogFetched(ILogger logger, int count, int pages, long elapsedMs);
}
