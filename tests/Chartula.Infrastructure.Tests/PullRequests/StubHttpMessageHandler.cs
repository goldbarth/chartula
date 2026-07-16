using System.Net;

namespace Chartula.Infrastructure.Tests.PullRequests;

/// <summary>
/// A stand-in <see cref="HttpMessageHandler"/> that records requests and returns
/// canned responses, so the reader is tested against mocked GitHub output with no
/// network call.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(responder(request));
    }

    public static StubHttpMessageHandler ReturningJson(string json)
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        });

    public static StubHttpMessageHandler ReturningStatus(HttpStatusCode status, string body = "")
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public static HttpClient ClientFor(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("https://api.github.com/") };
}
