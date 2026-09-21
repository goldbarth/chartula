using Chartula.Core.History;
using Chartula.Infrastructure.History;

namespace Chartula.Infrastructure.Tests.History;

public sealed class GitCliCommitReaderTests
{
    [Fact]
    public async Task Reads_only_the_commits_between_the_previous_tag_and_the_release_tag()
    {
        using TempGitRepository repo = new();
        repo.Commit("A");
        repo.Commit("B");
        repo.Tag("v1.0.0");
        repo.Commit("C");
        repo.Commit("D");
        repo.Tag("v2.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path)
            .ReadReleaseCommitsAsync("v2.0.0");

        Assert.Equal("v2.0.0", range.ToTag);
        Assert.Equal("v1.0.0", range.From);
        Assert.False(range.IsWholeHistory);

        string[] subjects = range.Commits.Select(c => c.Subject).ToArray();
        Assert.Equal(["D", "C"], subjects); // git log is newest-first
        Assert.DoesNotContain("A", subjects);
        Assert.DoesNotContain("B", subjects);
    }

    [Fact]
    public async Task Falls_back_to_all_history_when_there_is_no_previous_tag()
    {
        using TempGitRepository repo = new();
        repo.Commit("A");
        repo.Commit("B");
        repo.Tag("v1.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path)
            .ReadReleaseCommitsAsync("v1.0.0");

        Assert.Null(range.From);
        Assert.True(range.IsWholeHistory);
        Assert.Equal(["B", "A"], range.Commits.Select(c => c.Subject).ToArray());
    }

    [Fact]
    public async Task Each_commit_carries_a_full_sha_and_subject()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: add dark mode");
        repo.Tag("v1.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path)
            .ReadReleaseCommitsAsync("v1.0.0");

        CommitInfo commit = Assert.Single(range.Commits);
        Assert.Equal("feat: add dark mode", commit.Subject);
        Assert.Equal(40, commit.Sha.Length);
        Assert.Matches("^[0-9a-f]{40}$", commit.Sha);
    }

    [Fact]
    public async Task Throws_a_clear_error_for_an_unknown_tag()
    {
        using TempGitRepository repo = new();
        repo.Commit("A");
        repo.Tag("v1.0.0");

        GitCliCommitReader reader = new(GitExecutable.FromPath(), repo.Path);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadReleaseCommitsAsync("v9.9.9"));
        Assert.Contains("v9.9.9", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_a_blank_tag(string tag)
    {
        using TempGitRepository repo = new();
        GitCliCommitReader reader = new(GitExecutable.FromPath(), repo.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadReleaseCommitsAsync(tag));
    }

    [Fact]
    public async Task Reads_the_date_the_tag_was_made()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: add dark mode");
        repo.Tag("v1.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path).ReadReleaseCommitsAsync("v1.0.0");

        // A lightweight tag creator-dates to its commit, which is today's run.
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), range.TaggedAt);
    }

    [Fact]
    public async Task An_annotated_tag_is_dated_by_when_it_was_made()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: add dark mode");
        repo.Run("tag", "-a", "v1.0.0", "-m", "Release 1.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path).ReadReleaseCommitsAsync("v1.0.0");

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), range.TaggedAt);
    }

    [Fact]
    public async Task A_named_start_bounds_a_first_tag()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: A");
        repo.Commit("feat: B");
        repo.Commit("feat: C");
        repo.Tag("v1.0.0");

        GitCliCommitReader reader = new(GitExecutable.FromPath(), repo.Path);
        Assert.True((await reader.ReadReleaseCommitsAsync("v1.0.0")).IsWholeHistory);

        repo.Run("tag", "baseline", "v1.0.0~2");
        CommitRange range = await reader.ReadReleaseCommitsAsync("v1.0.0", since: "baseline");

        Assert.Equal("baseline", range.From);
        Assert.False(range.IsWholeHistory);
        Assert.Equal(["feat: C", "feat: B"], range.Commits.Select(c => c.Subject));
    }

    [Fact]
    public async Task A_named_start_overrides_the_previous_tag_and_accepts_a_commit()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: A");
        repo.Tag("v1.0.0");
        repo.Commit("feat: B");
        repo.Commit("feat: C");
        repo.Tag("v2.0.0");

        CommitRange range = await new GitCliCommitReader(GitExecutable.FromPath(), repo.Path)
            .ReadReleaseCommitsAsync("v2.0.0", since: "v2.0.0~1");

        Assert.Equal("v2.0.0~1", range.From);
        Assert.Equal(["feat: C"], range.Commits.Select(c => c.Subject));
    }

    [Fact]
    public async Task A_start_that_does_not_resolve_is_refused_by_name()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: A");
        repo.Tag("v1.0.0");

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitCliCommitReader(GitExecutable.FromPath(), repo.Path).ReadReleaseCommitsAsync("v1.0.0", since: "nope"));

        Assert.Contains("'nope' does not resolve", error.Message);
    }

    [Fact]
    public async Task A_start_that_is_not_behind_the_tag_is_refused()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: A");
        repo.Tag("v1.0.0");
        repo.Commit("feat: B");
        repo.Tag("later");

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitCliCommitReader(GitExecutable.FromPath(), repo.Path).ReadReleaseCommitsAsync("v1.0.0", since: "later"));

        Assert.Contains("not an ancestor of 'v1.0.0'", error.Message);
    }
}
