using Chartula.Infrastructure.History;

namespace Chartula.Infrastructure.Tests.History;

public sealed class GitCliRepositoryReaderTests
{
    [Fact]
    public async Task Reads_the_nearest_tag_reachable_from_head_even_after_later_commits()
    {
        using TempGitRepository repo = new();
        repo.Commit("A");
        repo.Tag("v1.0.0");
        repo.Commit("B");
        repo.Tag("v1.1.0");
        repo.Commit("C");

        Assert.Equal("v1.1.0", await new GitCliRepositoryReader(GitExecutable.FromPath(), repo.Path).ReadNearestTagAsync());
    }

    [Fact]
    public async Task Reads_no_tag_when_the_history_has_none()
    {
        using TempGitRepository repo = new();
        repo.Commit("A");

        Assert.Null(await new GitCliRepositoryReader(GitExecutable.FromPath(), repo.Path).ReadNearestTagAsync());
    }

    [Fact]
    public async Task Reads_the_url_of_the_named_remote()
    {
        using TempGitRepository repo = new();
        repo.Run("remote", "add", "origin", "git@github.com:owner/name.git");

        Assert.Equal("git@github.com:owner/name.git", await new GitCliRepositoryReader(GitExecutable.FromPath(), repo.Path).ReadRemoteUrlAsync("origin"));
    }

    [Fact]
    public async Task Reads_nothing_outside_a_git_repository()
    {
        string directory = Path.Combine(Path.GetTempPath(), "chartula-no-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            GitCliRepositoryReader reader = new(GitExecutable.FromPath(), directory);

            Assert.Null(await reader.ReadNearestTagAsync());
            Assert.Null(await reader.ReadRemoteUrlAsync("origin"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
