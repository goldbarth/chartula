using System.Net;
using Chartula.Core.History;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.PullRequests;

namespace Chartula.Infrastructure.Tests.PullRequests;

public sealed class GitHubPullRequestReaderTests
{
    private static readonly RepositoryCoordinates Repo = new("octo", "repo");

    private const string TwoPullsJson =
        """
        [
          {
            "number": 7,
            "title": "Add dark mode",
            "body": "Adds a dark theme.",
            "html_url": "https://github.com/octo/repo/pull/7",
            "merged_at": "2024-01-02T03:04:05Z",
            "labels": [ { "name": "feature" }, { "name": "ui" } ]
          },
          {
            "number": 8,
            "title": "WIP experiment",
            "body": "",
            "html_url": "https://github.com/octo/repo/pull/8",
            "merged_at": null,
            "labels": []
          }
        ]
        """;

    private static CommitRange RangeWith(params string[] shas)
        => new("v1.0.0", "v0.9.0", shas.Select(s => new CommitInfo(s, "subject")).ToArray());

    [Fact]
    public async Task Retrieves_merged_pull_requests_and_maps_every_field()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningJson(TwoPullsJson);
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        IReadOnlyList<PullRequestInfo> pulls = await reader.GetMergedPullRequestsAsync(
            Repo, RangeWith("abc123"));

        PullRequestInfo pull = Assert.Single(pulls); // #8 is not merged, so excluded
        Assert.Equal(7, pull.Number);
        Assert.Equal("Add dark mode", pull.Title);
        Assert.Equal("Adds a dark theme.", pull.Description);
        Assert.Equal(["feature", "ui"], pull.Labels);
        Assert.Equal("https://github.com/octo/repo/pull/7", pull.Url);

        Assert.Equal(
            "https://api.github.com/repos/octo/repo/commits/abc123/pulls",
            handler.Requests.Single().ToString());
    }

    [Fact]
    public async Task De_duplicates_a_pull_request_seen_across_multiple_commits()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningJson(TwoPullsJson);
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        IReadOnlyList<PullRequestInfo> pulls = await reader.GetMergedPullRequestsAsync(
            Repo, RangeWith("sha1", "sha2"));

        Assert.Equal(2, handler.Requests.Count); // one request per commit
        Assert.Equal(7, Assert.Single(pulls).Number); // still a single, de-duplicated PR
    }

    // RevertPairing matches a revert that names commits to pull requests through these commits (#206).
    [Fact]
    public async Task Records_every_commit_of_the_range_that_belongs_to_a_pull_request()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningJson(TwoPullsJson);
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        IReadOnlyList<PullRequestInfo> pulls = await reader.GetMergedPullRequestsAsync(
            Repo, RangeWith("sha1", "sha2"));

        Assert.Equal(["sha1", "sha2"], Assert.Single(pulls).CommitShas);
    }

    [Fact]
    public async Task Makes_no_request_and_returns_empty_for_a_range_with_no_commits()
    {
        StubHttpMessageHandler handler = new(_ => throw new Xunit.Sdk.XunitException("should not call the API"));
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        IReadOnlyList<PullRequestInfo> pulls = await reader.GetMergedPullRequestsAsync(
            Repo, RangeWith());

        Assert.Empty(pulls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Turns_an_API_error_into_a_clear_exception_rather_than_crashing()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningStatus(
            HttpStatusCode.InternalServerError, "boom");
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
        Assert.Contains("500", ex.Message);
        Assert.Contains("abc123", ex.Message);
    }

    [Fact]
    public async Task Turns_a_network_failure_into_a_clear_exception()
    {
        StubHttpMessageHandler handler = new(_ => throw new HttpRequestException("connection refused"));
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
        Assert.Contains("GitHub", ex.Message);
    }

    [Fact]
    public async Task Turns_malformed_JSON_into_a_clear_exception()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningJson("{ not json");
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
    }

    // #149: a --repo that does not exist, or that the request cannot see, returns 404
    // on the first request. The error names the repository, not a commit.
    [Fact]
    public async Task Names_the_repository_and_the_missing_token_for_a_404_on_the_first_request()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningStatus(
            HttpStatusCode.NotFound, NotFoundJson);
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "MY_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));

        Assert.StartsWith("GitHub cannot read octo/repo (404 Not Found).", ex.Message);
        Assert.Contains("--repo is misspelled", ex.Message);
        Assert.Contains("no token: MY_TOKEN is not set", ex.Message);
        Assert.DoesNotContain("abc123", ex.Message);
        Assert.DoesNotContain("documentation_url", ex.Message);
    }

    [Fact]
    public async Task Quotes_the_permission_GitHub_says_a_token_is_missing()
    {
        StubHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"message":"Resource not accessible by personal access token"}"""),
            };
            response.Headers.Add("x-accepted-github-permissions", "pull_requests=read");
            return response;
        });
        HttpClient client = StubHttpMessageHandler.ClientFor(handler);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "token");
        GitHubPullRequestReader reader = new(client, "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));

        Assert.StartsWith("GitHub cannot read octo/repo (403 Forbidden).", ex.Message);
        Assert.Contains("the token from GITHUB_TOKEN", ex.Message);
        Assert.Contains("GitHub says the token needs: pull_requests=read", ex.Message);
        Assert.Contains("lacks the Pull requests (read) permission", ex.Message);
    }

    // After a successful first request the repository is readable, so a later 404 is
    // about that commit, and the error names the commit.
    [Fact]
    public async Task Reports_a_later_404_against_its_commit_in_GitHubs_words()
    {
        int calls = 0;
        StubHttpMessageHandler handler = new(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TwoPullsJson) }
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(NotFoundJson) });
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("sha1", "sha2")));

        Assert.Equal("GitHub API returned 404 Not Found for commit sha2: Not Found", ex.Message);
    }

    [Fact]
    public async Task Names_the_checkout_or_an_unpushed_commit_for_a_commit_GitHub_does_not_know()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningStatus(
            HttpStatusCode.UnprocessableEntity, """{"message":"No commit found for SHA: abc123"}""");
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));

        Assert.StartsWith("Commit abc123 is not in octo/repo on GitHub", ex.Message);
        Assert.Contains("not a checkout of octo/repo", ex.Message);
        Assert.Contains("not been pushed", ex.Message);
    }

    [Fact]
    public async Task Names_the_token_variable_when_GitHub_rejects_the_token()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningStatus(
            HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");
        HttpClient client = StubHttpMessageHandler.ClientFor(handler);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "expired");
        GitHubPullRequestReader reader = new(client, "MY_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));

        Assert.Equal(
            "GitHub rejected the token in MY_TOKEN (401 Unauthorized): it is expired, revoked or mistyped.",
            ex.Message);
    }

    // An exhausted rate limit also returns 403, but is not a permission problem.
    [Fact]
    public async Task Reports_a_spent_rate_limit_as_such_rather_than_as_missing_access()
    {
        StubHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"message":"API rate limit exceeded"}"""),
            };
            response.Headers.Add("x-ratelimit-remaining", "0");
            return response;
        });
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler), "GITHUB_TOKEN");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));

        Assert.StartsWith("GitHub's rate limit is spent (403 Forbidden).", ex.Message);
        Assert.Contains("A token in GITHUB_TOKEN raises it", ex.Message);
    }

    private const string NotFoundJson =
        """{"message":"Not Found","documentation_url":"https://docs.github.com/rest","status":"404"}""";
}
