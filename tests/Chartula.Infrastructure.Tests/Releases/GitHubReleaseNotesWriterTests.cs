using System.Net;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.Releases;

namespace Chartula.Infrastructure.Tests.Releases;

public sealed class GitHubReleaseNotesWriterTests
{
    private static readonly RepositoryCoordinates Repo = new("octo", "repo");

    /// <summary>Records requests and routes responses by method + path.</summary>
    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string bodyText = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            LastBodyByMethod[request.Method] = bodyText;
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return route(request);
        }

        public Dictionary<HttpMethod, string> LastBodyByMethod { get; } = [];
    }

    private static HttpClient Client(RoutingHandler handler)
        => new(handler) { BaseAddress = new Uri("https://api.github.com/") };

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json) };

    [Fact]
    public async Task Creates_a_release_when_none_exists_for_the_tag()
    {
        RoutingHandler handler = new(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
            : Json(HttpStatusCode.Created, """{"id":1,"html_url":"https://github.com/octo/repo/releases/tag/v1.0.0"}"""));
        GitHubReleaseNotesWriter writer = new(Client(handler));

        string url = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        Assert.Equal("https://github.com/octo/repo/releases/tag/v1.0.0", url);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post); // created
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Contains("- Added search", handler.LastBodyByMethod[HttpMethod.Post]);
        Assert.Contains("v1.0.0", handler.LastBodyByMethod[HttpMethod.Post]); // tag_name in the body
    }

    [Fact]
    public async Task Updates_the_existing_release_rather_than_duplicating()
    {
        RoutingHandler handler = new(request => request.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, """{"id":42,"html_url":"https://github.com/octo/repo/releases/tag/v1.0.0"}""")
            : Json(HttpStatusCode.OK, """{"id":42,"html_url":"https://github.com/octo/repo/releases/tag/v1.0.0"}"""));
        GitHubReleaseNotesWriter writer = new(Client(handler));

        string url = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        Assert.Equal("https://github.com/octo/repo/releases/tag/v1.0.0", url);
        // Updates the found release (PATCH /releases/42), never creates a second one.
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Patch && r.Path.EndsWith("/releases/42"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains("- Added search", handler.LastBodyByMethod[HttpMethod.Patch]);
    }

    [Fact]
    public async Task Turns_an_API_error_into_a_clear_exception()
    {
        RoutingHandler handler = new(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
        GitHubReleaseNotesWriter writer = new(Client(handler));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Turns_a_network_failure_into_a_clear_exception()
    {
        RoutingHandler handler = new(_ => throw new HttpRequestException("connection refused"));
        GitHubReleaseNotesWriter writer = new(Client(handler));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));
        Assert.Contains("GitHub", ex.Message);
    }

    [Fact]
    public async Task Rejects_a_blank_tag()
    {
        GitHubReleaseNotesWriter writer = new(Client(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(Repo, "  ", "- x"));
    }
}
