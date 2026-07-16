using Chartula.Core.Curation;
using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Tests.Curation;

public sealed class ReleaseChangeResolverTests
{
    private readonly ReleaseChangeResolver _resolver = new();

    private static CommitRange Range(params (string Sha, string Subject)[] commits)
        => new("v1.0.0", "v0.9.0", commits.Select(c => new CommitInfo(c.Sha, c.Subject)).ToArray());

    private static PullRequestInfo Pull(
        int number, string title, string? body = null, params string[] labels)
        => new(number, title, body, labels, $"https://example/pull/{number}");

    [Fact]
    public void Uses_pull_requests_when_they_are_available()
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(
            Range(("sha1", "some commit")),
            [Pull(7, "Add dark mode", "A theme.", "feature")]);

        ReleaseChange change = Assert.Single(changes);
        Assert.Equal(ChangeSource.PullRequest, change.Source);
        Assert.Equal("Add dark mode", change.Title);
        Assert.Equal("A theme.", change.Description);
        Assert.Equal(7, change.Number);
        Assert.Equal(["feature"], change.Labels);
        Assert.Null(change.CommitSha);
    }

    [Fact]
    public void Falls_back_to_commit_data_when_there_are_no_pull_requests()
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(
            Range(("sha1", "feat: add search"), ("sha2", "fix: crash on start")),
            pullRequests: []);

        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Equal(ChangeSource.Commit, c.Source));
        Assert.Equal(["feat: add search", "fix: crash on start"], changes.Select(c => c.Title));
        Assert.Equal("sha1", changes[0].CommitSha);
        Assert.All(changes, c => Assert.Null(c.Number));
    }

    [Fact]
    public void Falls_back_to_the_body_when_the_pull_request_title_is_blank()
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(
            Range(),
            [Pull(7, "   ", body: "Adds a dark theme\nMore detail")]);

        Assert.Equal("Adds a dark theme", Assert.Single(changes).Title);
    }

    [Theory]
    [InlineData("WIP")]
    [InlineData("update")]
    [InlineData("Merge pull request #7 from feature/x")]
    public void Treats_uninformative_titles_as_unusable_and_uses_the_body(string title)
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(
            Range(),
            [Pull(7, title, body: "Adds a dark theme")]);

        Assert.Equal("Adds a dark theme", Assert.Single(changes).Title);
    }

    [Fact]
    public void Falls_back_to_the_pull_request_number_when_nothing_else_is_usable()
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(
            Range(),
            [Pull(42, title: "", body: "")]);

        Assert.Equal("PR #42", Assert.Single(changes).Title);
    }

    [Fact]
    public void Never_hard_fails_on_an_empty_release()
    {
        IReadOnlyList<ReleaseChange> changes = _resolver.Resolve(Range(), pullRequests: []);

        Assert.Empty(changes);
    }
}
