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

    // #73's body, verbatim: this repository's pull request template, untouched.
    internal const string UnfilledTemplate =
        """
        ## What does this PR do?

        <!-- A clear, changelog-style summary of the change. -->

        ## Why?

        <!-- The motivation. Link any related issue: Closes #123 -->

        ## Type of change

        - [x] Bug fix
        - [x] New feature
        - [x] Documentation
        - [x] Refactor / internal
        - [x] Breaking change

        ## Checklist

        - [x] The project builds and tests pass
        - [x] Code follows the formatting in `.editorconfig`
        - [x] I've added or updated tests where it makes sense
        - [x] I've updated documentation where relevant
        - [x] No secrets or API keys are included in this change
        """;

    [Fact]
    public void An_unfilled_template_is_no_description()
        => Assert.Null(Assert.Single(_resolver.Resolve(Range(), [Pull(73, "feat: config", UnfilledTemplate)])).Description);

    [Fact]
    public void An_unfilled_template_does_not_stand_in_for_an_uninformative_title()
        => Assert.Equal("PR #73", Assert.Single(_resolver.Resolve(Range(), [Pull(73, "WIP", UnfilledTemplate)])).Title);

    [Fact]
    public void A_filled_template_keeps_what_was_written_without_the_placeholders()
    {
        string body = UnfilledTemplate.Replace(
            "<!-- A clear, changelog-style summary of the change. -->",
            "<!-- A clear, changelog-style summary of the change. -->\nAdds a dark theme.");

        string? description = Assert.Single(_resolver.Resolve(Range(), [Pull(7, "feat: dark mode", body)])).Description;

        Assert.NotNull(description);
        Assert.Contains("Adds a dark theme.", description);
        Assert.Contains("## Type of change", description);
        Assert.DoesNotContain("<!--", description);
        Assert.DoesNotContain("Closes #123", description);
    }

    // A comment is invisible on GitHub: nobody reading the pull request sees it, so
    // it is not something the author told a reader.
    [Theory]
    [InlineData("Adds a theme.\n<!-- BREAKING CHANGE: not really -->", "Adds a theme.")]
    [InlineData("Adds a theme. <!-- inline --> Done.", "Adds a theme.  Done.")]
    [InlineData("Adds a theme.\n<!-- never closed\nBREAKING CHANGE: hidden", "Adds a theme.")]
    [InlineData("<!-- only a comment -->", null)]
    [InlineData("## Summary\n\n- [ ] Tests", null)]
    public void Hidden_comments_are_stripped_from_every_body(string body, string? expected)
        => Assert.Equal(expected, Assert.Single(_resolver.Resolve(Range(), [Pull(7, "feat: theme", body)])).Description);

    [Fact]
    public void Headings_and_checklists_stay_when_the_body_says_something_besides()
    {
        // Only a body with nothing else in it is a template; otherwise the headings
        // and boxes are part of what the author wrote, and curation does not edit it.
        string body = "## Summary\n\nAdds a theme.\n\n- [x] Tests";

        Assert.Equal(body, Assert.Single(_resolver.Resolve(Range(), [Pull(7, "feat: theme", body)])).Description);
    }
}
