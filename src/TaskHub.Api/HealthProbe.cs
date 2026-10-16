namespace TaskHub.Api;

/// <summary>Calls the local health endpoint and maps the answer to a process exit code (0 = healthy).</summary>
internal static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', ',')[0];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{(string.IsNullOrWhiteSpace(port) ? "8080" : port)}/api/health"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
