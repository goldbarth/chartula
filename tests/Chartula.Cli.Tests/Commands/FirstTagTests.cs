namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI from a checkout with only one tag, the first.
/// Its range is every commit up to the tag, which is confirmed before any request. The
/// process has no terminal, so without <c>--yes</c> the run must stop there. The named
/// repository does not exist on purpose: reaching GitHub would fail with a different error.
/// </summary>
public sealed class FirstTagTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-first-tag-" + Guid.NewGuid().ToString("N"));

    private async Task CreateCheckoutAsync()
    {
        Directory.CreateDirectory(_checkout);
        foreach (string[] git in (string[][])[
                     ["init", "-b", "main"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: A"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: B"],
                     ["tag", "v0.1.0"],
                     // After the tag, so a count of the whole history would be wrong.
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: C"]])
        {
            (int gitExit, _, string gitError) = await CliProcess.RunAsync("git", _checkout, git);
            Assert.True(gitExit == 0, gitError);
        }
    }

    [Fact]
    public async Task A_first_tag_without_a_terminal_stops_before_any_request_and_names_both_ways_on()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "sk-test-not-used" },
            "preview", "--tag", "v0.1.0", "--repo", "owner/does-not-exist");

        Assert.Equal(1, exitCode);
        Assert.Contains("Range:  every commit up to v0.1.0 (2 commits), the first tag", error);
        Assert.Contains("--since <ref>: the commits after <ref>, up to v0.1.0", error);
        Assert.Contains("Pass --yes to confirm it up front.", error);
        Assert.Contains("Stopped: The range of v0.1.0 was not confirmed", output);
        Assert.DoesNotContain("GitHub API", output + error);
    }

    // GitHub points at a closed local port, so passing the gate shows as a failed
    // request instead of reaching the network.
    [Fact]
    public async Task A_first_tag_confirmed_up_front_goes_on_to_github()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "sk-test-not-used",
                ["Chartula__GitHub__ApiBaseUrl"] = "https://127.0.0.1:1/",
            },
            "preview", "--tag", "v0.1.0", "--repo", "owner/does-not-exist", "--yes");

        Assert.Equal(1, exitCode);
        Assert.Contains("Range:  every commit up to v0.1.0 (2 commits), the first tag", error);
        Assert.DoesNotContain("Stopped:", output);
        Assert.DoesNotContain("[y/N]", error);
        Assert.Contains("127.0.0.1", output);
    }

    public void Dispose() => TestDirectory.Delete(_checkout);
}
