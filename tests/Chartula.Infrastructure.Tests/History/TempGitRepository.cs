using System.Diagnostics;

namespace Chartula.Infrastructure.Tests.History;

/// <summary>
/// A throwaway git repository on disk for exercising the real reader against real
/// git output. Deterministic and offline; deleted on dispose.
/// </summary>
internal sealed class TempGitRepository : IDisposable
{
    public string Path { get; }

    public TempGitRepository()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "chartula-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Run("init", "-b", "main");
        Run("config", "user.email", "test@example.com");
        Run("config", "user.name", "Chartula Test");
        Run("config", "commit.gpgsign", "false");
    }

    public void Commit(string subject) => Run("commit", "--allow-empty", "-m", subject);

    public void Tag(string name) => Run("tag", name);

    public void Run(params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git.");
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed: {error}");
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; a leftover temp dir must not fail a test.
        }
    }
}
