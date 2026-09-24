namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI from a checkout with only one tag, the first.
/// Without a start, the run must stop before any request. The named repository does
/// not exist on purpose: reaching GitHub would fail with a different error.
/// </summary>
public sealed class FirstTagTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-first-tag-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_first_tag_asks_where_the_release_starts()
    {
        Directory.CreateDirectory(_checkout);
        foreach (string[] git in (string[][])[
                     ["init", "-b", "main"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: A"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: B"],
                     ["tag", "v0.1.0"]])
        {
            (int gitExit, _, string gitError) = await CliProcess.RunAsync("git", _checkout, git);
            Assert.True(gitExit == 0, gitError);
        }

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "sk-test-not-used" },
            "preview", "--tag", "v0.1.0", "--repo", "owner/does-not-exist");

        Assert.Equal(1, exitCode);
        Assert.Contains("v0.1.0 is the first tag, so its range is the whole history (2 commits).", output);
        Assert.Contains("--since <ref>", output);
        Assert.Contains("--whole-history", output);
        Assert.DoesNotContain("GitHub API", output + error);
    }

    public void Dispose() => TestDirectory.Delete(_checkout);
}
