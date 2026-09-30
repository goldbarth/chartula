using Chartula.Cli.Composition;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI with <c>--version</c>, the way a user filling in a bug report does.
/// It needs no checkout, no key and no token, so it runs from an empty directory.
/// </summary>
public sealed class VersionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("chartula-version-").FullName;

    [Fact]
    public async Task Version_prints_the_version_with_its_commit_and_succeeds()
    {
        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _directory,
            new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = null, ["GITHUB_TOKEN"] = null },
            "--version");

        Assert.Equal(0, exitCode);
        Assert.Equal($"chartula {ToolVersion.Informational}", output.TrimEnd());
        Assert.StartsWith($"chartula {ToolVersion.Release}+", output);
        Assert.Empty(error);
    }

    [Fact]
    public void The_help_text_names_it() => Assert.Contains("chartula --version", Program.Usage);

    public void Dispose() => TestDirectory.Delete(_directory);
}
