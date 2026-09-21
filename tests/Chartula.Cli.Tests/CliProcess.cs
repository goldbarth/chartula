using System.Diagnostics;

namespace Chartula.Cli.Tests;

/// <summary>
/// Starts the built CLI the way a user does: its own process, from a directory the
/// test chose, with the environment it inherits plus what the test sets.
/// </summary>
internal static class CliProcess
{
    public static Task<CliResult> RunChartulaAsync(
        string directory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        string chartula = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "chartula.exe" : "chartula");
        ProcessStartInfo startInfo = Start(chartula, directory, arguments);
        foreach ((string name, string? value) in environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        return RunAsync(startInfo);
    }

    public static Task<CliResult> RunAsync(string fileName, string directory, params string[] arguments)
        => RunAsync(Start(fileName, directory, arguments));

    private static ProcessStartInfo Start(string fileName, string directory, string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = directory,
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

    private static async Task<CliResult> RunAsync(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, await output, await error);
    }
}

/// <summary>What a process left behind: its exit code, stdout and stderr.</summary>
internal sealed record CliResult(int ExitCode, string Output, string Error);
