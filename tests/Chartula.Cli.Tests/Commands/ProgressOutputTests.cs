namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #283: runs the built CLI without a terminal. Its progress goes to stderr as plain
/// lines, stdout carries what it always did, and a step that fails is the last one
/// shown before the error. GitHub points at a closed local port, so the run stops in
/// the step that reads the pull requests.
/// </summary>
public sealed class ProgressOutputTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-progress-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Progress_goes_to_stderr_as_plain_lines_and_ends_with_the_step_that_failed()
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
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "sk-test-not-used",
                ["Chartula__GitHub__ApiBaseUrl"] = "https://127.0.0.1:1/",
            },
            "generate", "--no-publish", "--tag", "v0.1.0", "--repo", "owner/does-not-exist", "--yes");

        Assert.Equal(1, exitCode);
        Assert.EndsWith("Reading pull requests (2 commits)", error.ReplaceLineEndings("\n").TrimEnd('\n'));
        Assert.DoesNotContain('\r', error.Replace(Environment.NewLine, "\n"));
        Assert.DoesNotContain('\u001b', error);
        Assert.StartsWith("Error: Could not reach the GitHub API", output);
        Assert.DoesNotContain("Reading pull requests", output);
    }

    public void Dispose() => TestDirectory.Delete(_checkout);
}
