using Chartula.Cli.Commands;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Tests.Commands;

public sealed class ReleaseTargetTests
{
    private const string Directory = "/work/my-repo";

    private static Func<Task<string?>> Returns(string? value) => () => Task.FromResult(value);

    private static Func<Task<string?>> NeverRead
        => () => throw new InvalidOperationException("A flag that was passed must not be read from git.");

    [Theory]
    [InlineData("https://github.com/owner/name.git")]
    [InlineData("https://github.com/owner/name")]
    [InlineData("https://github.com/owner/name/")]
    [InlineData("https://user@github.com/owner/name.git")]
    [InlineData("git@github.com:owner/name.git")]
    [InlineData("git@github.com:owner/name")]
    [InlineData("ssh://git@github.com/owner/name.git")]
    [InlineData("ssh://git@github.example.com:2222/owner/name.git")]
    public void Reads_owner_and_name_from_a_remote_url(string url)
    {
        Assert.True(ReleaseTarget.TryParseRemoteUrl(url, out RepositoryCoordinates repository));
        Assert.Equal(new RepositoryCoordinates("owner", "name"), repository);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/home/me/repos/name")]
    [InlineData("file:///home/me/repos/name.git")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://gitlab.com/group/subgroup/name.git")]
    public void Refuses_a_remote_url_that_does_not_name_owner_and_name(string url)
    {
        Assert.False(ReleaseTarget.TryParseRemoteUrl(url, out _));
    }

    [Fact]
    public async Task Takes_both_flags_as_given_without_reading_git_or_announcing_anything()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview", "--tag", "v2.0.0", "--repo", "owner/name"], Directory, NeverRead, NeverRead, error);

        Assert.Equal(new ReleaseTarget("v2.0.0", new RepositoryCoordinates("owner", "name")), target);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task Defaults_both_from_the_checkout_and_says_where_they_came_from()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview"], Directory, Returns("v1.3.0"), Returns("git@github.com:owner/name.git"), error);

        Assert.Equal(new ReleaseTarget("v1.3.0", new RepositoryCoordinates("owner", "name")), target);
        Assert.Contains("Using tag v1.3.0", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Using repository owner/name", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_tag_names_the_flag_and_the_directory_it_looked_in()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview", "--repo", "owner/name"], Directory, Returns(null), NeverRead, error);

        Assert.Null(target);
        Assert.Contains("--tag", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(Directory, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_remote_names_the_flag_and_the_directory_it_looked_in()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview", "--tag", "v1.0.0"], Directory, NeverRead, Returns(null), error);

        Assert.Null(target);
        Assert.Contains("--repo", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(Directory, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_remote_that_names_no_repository_is_quoted_in_the_error()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview", "--tag", "v1.0.0"], Directory, NeverRead, Returns("/home/me/repos/name"), error);

        Assert.Null(target);
        Assert.Contains("/home/me/repos/name", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_invalid_repo_flag_is_refused_rather_than_replaced_by_the_remote()
    {
        StringWriter error = new();

        ReleaseTarget? target = await ReleaseTarget.ResolveAsync(
            ["preview", "--tag", "v1.0.0", "--repo", "name-only"], Directory, NeverRead, NeverRead, error);

        Assert.Null(target);
        Assert.Contains("Invalid option --repo 'name-only'", error.ToString(), StringComparison.Ordinal);
    }
}
