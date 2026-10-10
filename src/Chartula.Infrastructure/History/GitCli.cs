using System.Diagnostics;

namespace Chartula.Infrastructure.History;

/// <summary>
/// Runs the <c>git</c> CLI in a directory.
/// Every reader that calls git uses it, so a git that cannot start fails with the same
/// message wherever it is first needed.
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
            // No command a run calls reads stdin, and it is redirected all the same. A child
            // that inherits it counts for the .NET runtime as one that uses the terminal, and
            // the runtime sets the terminal up again when that child exits: its keypad mode
            // sequence, once more after every git call (#348).
            RedirectStandardInput = true,
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
        // Closed before git is waited for, so a git that does ask for input reads the end
        // of it and fails, instead of waiting on a pipe nobody writes to.
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken);

        return new GitResult(process.ExitCode, await standardOutput, await standardError);
    }
}

internal readonly record struct GitResult(int ExitCode, string StandardOutput, string StandardError);
