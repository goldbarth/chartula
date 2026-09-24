namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI the way a user does, from a checkout that contains its own <c>git</c>.
/// The run starts without <c>--tag</c> and <c>--repo</c>, in a checkout with a remote
/// and no tag. With the real git, it stops at "no tag is reachable" before any network call.
/// If the planted git ran instead, the remote would be unreadable, and the run would
/// stop one message earlier.
/// </summary>
public sealed class PlantedGitTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-planted-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_git_committed_to_the_checkout_is_not_run()
    {
        Directory.CreateDirectory(_checkout);
        await GitAsync("init", "-b", "main");
        await GitAsync("remote", "add", "origin", "git@github.com:owner/name.git");
        string marker = Plant();

        (int exitCode, _, string error) = await RunChartulaAsync("preview");

        Assert.False(File.Exists(marker), "The planted git ran.");
        Assert.Equal(1, exitCode);
        Assert.Contains("no tag is reachable from HEAD", error);
    }

    public void Dispose() => TestDirectory.Delete(_checkout);

    /// <summary>
    /// Plants a git in the checkout and returns the file the planted git creates when run.
    /// Windows starts only <c>git.exe</c> by bare name. So on Windows the plant is a copy
    /// of a system binary that creates no file but fails every git command.
    /// </summary>
    private string Plant()
    {
        string marker = Path.Combine(_checkout, "planted-git-ran");
        if (OperatingSystem.IsWindows())
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "whoami.exe"), Path.Combine(_checkout, "git.exe"));
        }
        else
        {
            string git = Path.Combine(_checkout, "git");
            File.WriteAllText(git, $"#!/bin/sh\ntouch '{marker}'\nexit 1\n");
            File.SetUnixFileMode(git, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return marker;
    }

    private Task<CliResult> RunChartulaAsync(params string[] arguments)
        => CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?>
            {
                // A dummy key, so the run passes the missing-key refusal, which comes before git.
                ["ANTHROPIC_API_KEY"] = "sk-test-not-used",
                ["GITHUB_TOKEN"] = null,
            },
            arguments);

    // Calling the test's own git by bare name is safe: it runs from the test's directory,
    // before the plant exists.
    private async Task GitAsync(params string[] arguments)
    {
        (int exitCode, _, string error) = await CliProcess.RunAsync("git", _checkout, arguments);
        Assert.True(exitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
    }
}
