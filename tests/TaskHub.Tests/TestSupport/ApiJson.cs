using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskHub.Tests.TestSupport;

internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Options, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"Empty body from {url}");
    }
}
