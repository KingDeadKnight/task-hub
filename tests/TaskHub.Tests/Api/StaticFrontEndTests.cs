using System.Net;
using Microsoft.AspNetCore.Hosting;
using TaskHub.Tests.TestSupport;

namespace TaskHub.Tests.Api;

/// <summary>The API serves the built front end from wwwroot (as in the Docker image).</summary>
public sealed class StaticFrontEndTests : IAsyncLifetime
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"taskhub-wwwroot-{Guid.NewGuid():N}");
    private readonly TaskHubApiFactory _root = new();
    private HttpClient _client = null!; // Assigned in InitializeAsync, before any test runs.

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><div id=\"root\"></div>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "console.log('app')");
        _client = _root.WithWebHostBuilder(b => b.UseWebRoot(_webRoot)).CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _root.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }

    private Task<HttpResponseMessage> GetAsync(string path) =>
        _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Assets_are_served_as_files_not_as_the_spa_page()
    {
        var response = await GetAsync("/assets/app.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("console.log");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/some/client/route")]
    public async Task Root_and_client_routes_serve_index_html(string path)
    {
        var response = await GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("/api/unknown")]
    [InlineData("/openapi/unknown.json")]
    public async Task Unknown_api_paths_stay_404(string path)
    {
        (await GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Api_still_works_alongside_static_files()
    {
        (await GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
