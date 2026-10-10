using Chartula.Infrastructure.History;

namespace Chartula.Infrastructure.Tests.History;

public sealed class GitCliTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "chartula-git-stdin-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Gives_git_a_closed_stdin_of_its_own_instead_of_the_one_the_run_has()
    {
        // No git command a run calls reads stdin, so nothing else notices when the redirect
        // is dropped: the suite runs without a terminal, where the sequence of #348 is never
        // written. /proc says what a descriptor is open on, and only Linux has it.
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // A stand-in git that names its stdin and then reads it to the end, which it only
        // reaches when the other end is closed.
        Directory.CreateDirectory(_root);
        string standIn = Path.Combine(_root, "git");
        File.WriteAllText(standIn, "#!/bin/sh\nreadlink /proc/self/fd/0\ncat > /dev/null\n");
        File.SetUnixFileMode(standIn, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        GitCliRepositoryReader reader = new(GitExecutable.Resolve(_root), _root);
        using CancellationTokenSource stillOpen = new(TimeSpan.FromSeconds(30));

        string? stdin = await reader.ReadRemoteUrlAsync("origin", stillOpen.Token);

        Assert.StartsWith("pipe:", stdin);
        Assert.NotEqual(new FileInfo("/proc/self/fd/0").LinkTarget, stdin);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
