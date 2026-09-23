namespace Chartula.Infrastructure.History;

/// <summary>
/// The git binary a run calls, resolved once to an absolute path.
/// Started by bare name, .NET's <c>Process</c> searches the application directory and
/// the current directory before <c>PATH</c>. A run's current directory is the checkout
/// it reads. So an executable named <c>git</c> committed to that repository would run
/// with the operator's environment, keys included.
/// </summary>
public sealed record GitExecutable
{
    private GitExecutable(string path) => Path = path;

    /// <summary>The absolute path of the binary.</summary>
    public string Path { get; }

    /// <summary>Resolves git from the process's <c>PATH</c>.</summary>
    public static GitExecutable FromPath() => Resolve(Environment.GetEnvironmentVariable("PATH"));

    /// <summary>
    /// The first <c>git</c> (<c>git.exe</c> on Windows) in an absolute directory of
    /// <paramref name="pathVariable"/>.
    /// Relative entries such as <c>.</c> and empty entries are skipped: they resolve
    /// against the current directory, the hole this type closes.
    /// </summary>
    public static GitExecutable Resolve(string? pathVariable)
    {
        string fileName = OperatingSystem.IsWindows() ? "git.exe" : "git";
        foreach (string directory in (pathVariable ?? string.Empty).Split(
                     System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.IO.Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            string candidate = System.IO.Path.Combine(directory, fileName);
            if (IsExecutable(candidate))
            {
                return new GitExecutable(candidate);
            }
        }

        throw new InvalidOperationException(
            "Could not find 'git' on the PATH. Chartula reads the release history with the git CLI - " +
            "install Git and make sure its directory is on the PATH. " +
            "Relative PATH entries such as '.' are ignored, so a git inside the repository is never run.");
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // Windows has no execute bit. The .exe name makes the file runnable.
        return OperatingSystem.IsWindows()
            || (File.GetUnixFileMode(path)
                & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }
}
