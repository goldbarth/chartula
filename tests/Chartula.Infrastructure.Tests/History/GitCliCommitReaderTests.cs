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

        CommitRange range = await new GitCliCommitReader(repo.Path)
            .ReadReleaseCommitsAsync("v2.0.0");

        Assert.Equal("v2.0.0", range.ToTag);
        Assert.Equal("v1.0.0", range.FromTag);
        Assert.False(range.IsFirstRelease);

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

        CommitRange range = await new GitCliCommitReader(repo.Path)
            .ReadReleaseCommitsAsync("v1.0.0");

        Assert.Null(range.FromTag);
        Assert.True(range.IsFirstRelease);
        Assert.Equal(["B", "A"], range.Commits.Select(c => c.Subject).ToArray());
    }

    [Fact]
    public async Task Each_commit_carries_a_full_sha_and_subject()
    {
        using TempGitRepository repo = new();
        repo.Commit("feat: add dark mode");
        repo.Tag("v1.0.0");

        CommitRange range = await new GitCliCommitReader(repo.Path)
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

        GitCliCommitReader reader = new(repo.Path);

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
        GitCliCommitReader reader = new(repo.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadReleaseCommitsAsync(tag));
    }
}
