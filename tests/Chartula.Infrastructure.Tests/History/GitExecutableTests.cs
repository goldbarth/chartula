using Chartula.Infrastructure.History;

namespace Chartula.Infrastructure.Tests.History;

public sealed class GitExecutableTests : IDisposable
{
    private static readonly string FileName = OperatingSystem.IsWindows() ? "git.exe" : "git";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "chartula-git-path-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolves_the_first_git_on_the_path_to_an_absolute_path()
    {
        string empty = Directory("empty");
        string first = DirectoryWithGit("first");
        DirectoryWithGit("second");

        GitExecutable git = GitExecutable.Resolve(PathOf(empty, first, Path.Combine(_root, "second")));

        Assert.Equal(Path.Combine(first, FileName), git.Path);
    }

    [Fact]
    public void Skips_relative_path_entries_even_when_they_hold_a_git()
    {
        // "." and an empty entry both mean the current directory. That is the checkout a
        // run reads, the one place git must never come from.
        // The planted git lives under the current directory and is named relative to it.
        // On Windows, a relative path to the temp root is impossible when the two are on
        // different drives.
        string relative = "chartula-planted-" + Guid.NewGuid().ToString("N");
        string planted = System.IO.Path.GetFullPath(relative);
        System.IO.Directory.CreateDirectory(planted);
        try
        {
            WriteGit(planted);
            string real = DirectoryWithGit("real");

            GitExecutable git = GitExecutable.Resolve(PathOf(".", "", relative, real));

            Assert.Equal(Path.Combine(real, FileName), git.Path);
        }
        finally
        {
            System.IO.Directory.Delete(planted, recursive: true);
        }
    }

    [Fact]
    public void Skips_a_git_that_is_not_executable()
    {
        // Windows has no execute bit, so there is nothing to skip there.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string plain = Directory("plain");
        File.WriteAllText(Path.Combine(plain, FileName), string.Empty);
        File.SetUnixFileMode(Path.Combine(plain, FileName), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        string real = DirectoryWithGit("real");

        Assert.Equal(Path.Combine(real, FileName), GitExecutable.Resolve(PathOf(plain, real)).Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Fails_naming_the_path_when_no_git_is_found(string? pathVariable)
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => GitExecutable.Resolve(pathVariable));

        Assert.Contains("PATH", ex.Message);
    }

    [Fact]
    public void Resolves_the_git_this_machine_has()
    {
        GitExecutable git = GitExecutable.FromPath();

        Assert.True(Path.IsPathFullyQualified(git.Path));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_root))
        {
            System.IO.Directory.Delete(_root, recursive: true);
        }
    }

    private static string PathOf(params string[] directories) => string.Join(Path.PathSeparator, directories);

    private string Directory(string name)
    {
        string directory = Path.Combine(_root, name);
        System.IO.Directory.CreateDirectory(directory);
        return directory;
    }

    private string DirectoryWithGit(string name)
    {
        string directory = Directory(name);
        WriteGit(directory);
        return directory;
    }

    private static void WriteGit(string directory)
    {
        string git = Path.Combine(directory, FileName);
        File.WriteAllText(git, string.Empty);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(git, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
