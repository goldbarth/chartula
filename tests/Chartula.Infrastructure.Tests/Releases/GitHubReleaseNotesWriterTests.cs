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
            ? request.RequestUri!.AbsolutePath.EndsWith("/releases")
                ? Json(HttpStatusCode.OK, "[]")
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
            : Json(HttpStatusCode.Created, """{"id":1,"html_url":"https://github.com/octo/repo/releases/tag/v1.0.0"}"""));
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

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
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        string url = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        Assert.Equal("https://github.com/octo/repo/releases/tag/v1.0.0", url);
        // Updates the found release (PATCH /releases/42), never creates a second one.
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Patch && r.Path.EndsWith("/releases/42"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains("- Added search", handler.LastBodyByMethod[HttpMethod.Patch]);
    }

    [Fact]
    public async Task Creates_a_new_release_as_a_draft()
    {
        RoutingHandler handler = new(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.Created, """{"id":1,"html_url":"https://github.com/octo/repo/releases/tag/untagged-1","draft":true}""")
            : request.RequestUri!.AbsolutePath.EndsWith("/releases")
                ? Json(HttpStatusCode.OK, "[]")
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        string written = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        // Generated text is a draft a person reads before it goes public.
        Assert.Contains("\"draft\":true", handler.LastBodyByMethod[HttpMethod.Post]);
        Assert.Equal("https://github.com/octo/repo/releases/tag/untagged-1 (draft)", written);
    }

    [Fact]
    public async Task Updates_an_existing_draft_rather_than_creating_another()
    {
        // GitHub's lookup by tag answers only published releases, so a draft from an
        // earlier run has to be found in the release list.
        RoutingHandler handler = new(request => request.Method == HttpMethod.Get
            ? request.RequestUri!.AbsolutePath.EndsWith("/releases")
                ? Json(HttpStatusCode.OK, """[{"id":5,"tag_name":"v0.9.0","draft":false},{"id":7,"tag_name":"v1.0.0","draft":true,"html_url":"https://github.com/octo/repo/releases/tag/untagged-7"}]""")
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
            : Json(HttpStatusCode.OK, """{"id":7,"html_url":"https://github.com/octo/repo/releases/tag/untagged-7","draft":true}"""));
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        string written = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Patch && r.Path.EndsWith("/releases/7"));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);

        // Without the tag GitHub detaches the draft from it, and the next run cannot find it.
        Assert.Contains("\"tag_name\":\"v1.0.0\"", handler.LastBodyByMethod[HttpMethod.Patch]);
        Assert.Equal("https://github.com/octo/repo/releases/tag/untagged-7 (draft)", written);
    }

    [Fact]
    public async Task Updating_a_published_release_leaves_it_published()
    {
        RoutingHandler handler = new(_ =>
            Json(HttpStatusCode.OK, """{"id":42,"html_url":"https://github.com/octo/repo/releases/tag/v1.0.0","draft":false}"""));
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        string written = await writer.WriteAsync(Repo, "v1.0.0", "- Added search");

        Assert.DoesNotContain("draft", handler.LastBodyByMethod[HttpMethod.Patch]);
        Assert.Equal("https://github.com/octo/repo/releases/tag/v1.0.0", written);
    }

    [Fact]
    public async Task Turns_an_API_error_into_a_clear_exception()
    {
        RoutingHandler handler = new(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Turns_a_network_failure_into_a_clear_exception()
    {
        RoutingHandler handler = new(_ => throw new HttpRequestException("connection refused"));
        GitHubReleaseNotesWriter writer = new(Client(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));
        Assert.Contains("GitHub", ex.Message);
    }

    [Fact]
    public async Task Rejects_a_blank_tag()
    {
        GitHubReleaseNotesWriter writer = new(Client(new RoutingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))), "GITHUB_TOKEN");

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(Repo, "  ", "- x"));
    }

    // #219: reading needs no write access, so the first request that needs it -
    // creating the draft - is where a read-only token is refused.
    private static RoutingHandler RefusingCreate(HttpStatusCode status, string? neededPermission = null)
        => new(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return request.RequestUri!.AbsolutePath.EndsWith("/releases")
                    ? Json(HttpStatusCode.OK, "[]")
                    : Json(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            }

            HttpResponseMessage refused = Json(status, """{"message":"Resource not accessible by personal access token","documentation_url":"https://docs.github.com"}""");
            if (neededPermission is not null)
            {
                refused.Headers.Add("x-accepted-github-permissions", neededPermission);
            }

            return refused;
        });

    [Fact]
    public async Task A_token_without_write_access_is_named_with_the_permission_GitHub_asks_for()
    {
        HttpClient client = Client(RefusingCreate(HttpStatusCode.Forbidden, "contents=write"));
        client.DefaultRequestHeaders.Authorization = new("Bearer", "read-only");
        GitHubReleaseNotesWriter writer = new(client, "MY_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));

        Assert.StartsWith("GitHub refused to publish the release notes for v1.0.0 to octo/repo (403 Forbidden).", ex.Message);
        Assert.Contains("the token from MY_TOKEN", ex.Message);
        Assert.Contains("GitHub says the token needs: contents=write", ex.Message);
        Assert.Contains("Contents read and write on octo/repo", ex.Message);
        Assert.DoesNotContain("documentation_url", ex.Message);
    }

    [Fact]
    public async Task Publishing_without_a_token_says_one_is_needed()
    {
        GitHubReleaseNotesWriter writer = new(Client(RefusingCreate(HttpStatusCode.Unauthorized)), "MY_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));

        Assert.Contains("(401 Unauthorized)", ex.Message);
        Assert.Contains("no token: MY_TOKEN is not set", ex.Message);
    }

    [Fact]
    public async Task Any_other_error_is_reported_in_GitHubs_words_against_the_tag()
    {
        GitHubReleaseNotesWriter writer = new(Client(RefusingCreate(HttpStatusCode.UnprocessableEntity)), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(Repo, "v1.0.0", "- x"));

        Assert.Equal(
            "GitHub API returned 422 Unprocessable Entity for release 'v1.0.0': Resource not accessible by personal access token",
            ex.Message);
    }
}
