namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI from a shallow clone, the checkout CI makes by default, whose
/// release tag has a predecessor beyond the fetch depth. It has to stop before any
/// request, which is why the repository named does not exist: reaching GitHub would
/// fail differently.
/// </summary>
public sealed class ShallowCloneTests : IDisposable
{
    private readonly string _origin = Path.Combine(Path.GetTempPath(), "chartula-shallow-origin-" + Guid.NewGuid().ToString("N"));
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-shallow-" + Guid.NewGuid().ToString("N"));

    // #243: --whole-history is refused as well, since the history it would render
    // is not the whole history.
    [Theory]
    [InlineData]
    [InlineData("--whole-history")]
    public async Task A_shallow_clone_is_refused_with_the_way_to_fetch_its_history(params string[] start)
    {
        Directory.CreateDirectory(_origin);
        foreach (string[] git in (string[][])[
                     ["init", "-b", "main"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: A"],
                     ["tag", "v0.1.0"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: B"],
                     ["tag", "v0.2.0"]])
        {
            (int gitExit, _, string gitError) = await CliProcess.RunAsync("git", _origin, git);
            Assert.True(gitExit == 0, gitError);
        }

        (int cloneExit, _, string cloneError) = await CliProcess.RunAsync(
            "git", _origin, "clone", "--quiet", "--depth", "1", "--branch", "v0.2.0", new Uri(_origin).AbsoluteUri, _checkout);
        Assert.True(cloneExit == 0, cloneError);

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "sk-test-not-used" },
            ["preview", "--tag", "v0.2.0", "--repo", "owner/does-not-exist", .. start]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error: The checkout is a shallow clone", output);
        Assert.Contains("git fetch --unshallow --tags", output);
        Assert.Contains("fetch-depth: 0", output);
        Assert.Contains("GIT_DEPTH: 0", output);
        Assert.DoesNotContain("first tag", output);
        Assert.DoesNotContain("GitHub API", output + error);
    }

    public void Dispose()
    {
        TestDirectory.Delete(_origin);
        TestDirectory.Delete(_checkout);
    }
}
