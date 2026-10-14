using System.Net;
using System.Text;

namespace TaskHub.Tests.TestSupport;

/// <summary>Records requests (with their body) and answers through a delegate.</summary>
internal sealed class StubHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), body);
        Requests.Add(recorded);
        return respond(recorded);
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Authorization, string Body);
