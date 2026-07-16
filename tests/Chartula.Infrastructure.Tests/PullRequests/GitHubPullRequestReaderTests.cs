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
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

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
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

        IReadOnlyList<PullRequestInfo> pulls = await reader.GetMergedPullRequestsAsync(
            Repo, RangeWith("sha1", "sha2"));

        Assert.Equal(2, handler.Requests.Count); // one request per commit
        Assert.Equal(7, Assert.Single(pulls).Number); // still a single, de-duplicated PR
    }

    [Fact]
    public async Task Makes_no_request_and_returns_empty_for_a_range_with_no_commits()
    {
        StubHttpMessageHandler handler = new(_ => throw new Xunit.Sdk.XunitException("should not call the API"));
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

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
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
        Assert.Contains("500", ex.Message);
        Assert.Contains("abc123", ex.Message);
    }

    [Fact]
    public async Task Turns_a_network_failure_into_a_clear_exception()
    {
        StubHttpMessageHandler handler = new(_ => throw new HttpRequestException("connection refused"));
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
        Assert.Contains("GitHub", ex.Message);
    }

    [Fact]
    public async Task Turns_malformed_JSON_into_a_clear_exception()
    {
        StubHttpMessageHandler handler = StubHttpMessageHandler.ReturningJson("{ not json");
        GitHubPullRequestReader reader = new(StubHttpMessageHandler.ClientFor(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.GetMergedPullRequestsAsync(Repo, RangeWith("abc123")));
    }
}
