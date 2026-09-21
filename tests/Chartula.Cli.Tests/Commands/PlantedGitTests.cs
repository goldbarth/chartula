using System.Diagnostics;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI the way a user does, from a checkout that carries its own
/// <c>git</c>. The run is started without <c>--tag</c> and <c>--repo</c> in a
/// checkout with a remote and no tag, so it stops at "no tag is reachable" before
/// any network call - which only the real git can get it to. A planted git that
/// ran instead would leave the remote unreadable and stop one message earlier.
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

        (int exitCode, string error) = await RunChartulaAsync("preview");

        Assert.False(File.Exists(marker), "The planted git ran.");
        Assert.Equal(1, exitCode);
        Assert.Contains("no tag is reachable from HEAD", error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_checkout))
        {
            Directory.Delete(_checkout, recursive: true);
        }
    }

    /// <summary>
    /// Plants a git in the checkout and returns the file it leaves behind when run.
    /// Windows starts only <c>git.exe</c> by bare name, so there it is a copy of a
    /// system binary that leaves no file but fails every git command it is given.
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

    private async Task<(int ExitCode, string Error)> RunChartulaAsync(params string[] arguments)
    {
        string chartula = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "chartula.exe" : "chartula");
        ProcessStartInfo startInfo = Start(chartula, arguments);

        // Past the refusal of a run without a key, which comes before git is reached.
        startInfo.Environment["ANTHROPIC_API_KEY"] = "sk-test-not-used";
        startInfo.Environment.Remove("GITHUB_TOKEN");
        return await RunAsync(startInfo);
    }

    // The test's own git by bare name is fine: it runs from the test's directory,
    // before the plant exists.
    private async Task GitAsync(params string[] arguments)
    {
        (int exitCode, string error) = await RunAsync(Start("git", arguments));
        Assert.True(exitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
    }

    private ProcessStartInfo Start(string fileName, string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = _checkout,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        return (process.ExitCode, await error);
    }
}
