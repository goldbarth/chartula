using System.Diagnostics;

namespace Chartula.Infrastructure.History;

/// <summary>
/// Runs the <c>git</c> CLI in a directory. Shared by every reader that shells out,
/// so a git that cannot start fails with the same message wherever it is first needed.
/// </summary>
internal static class GitCli
{
    public static async Task<GitResult> RunAsync(
        GitExecutable git,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = git.Path,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start git at '{git.Path}'.", ex);
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new GitResult(process.ExitCode, await standardOutput, await standardError);
    }
}

internal readonly record struct GitResult(int ExitCode, string StandardOutput, string StandardError);
