using Chartula.Infrastructure.Serialization;

namespace Chartula.Infrastructure.Tests.Serialization;

public sealed class FileChangelogMarkdownWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "chartula-md-" + Guid.NewGuid().ToString("N"));

    private string Path_ => System.IO.Path.Combine(_directory, "CHANGELOG.md");

    [Fact]
    public async Task Writes_a_new_changelog_file()
    {
        FileChangelogMarkdownWriter writer = new(_directory);

        string path = await writer.WriteAsync("v1.0.0", "- Added search");

        Assert.Equal(Path_, path);
        Assert.Equal("# Changelog\n\n## v1.0.0\n\n- Added search\n", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Prepends_across_runs_and_preserves_earlier_releases()
    {
        FileChangelogMarkdownWriter writer = new(_directory);

        await writer.WriteAsync("v1.0.0", "- Added search");
        await writer.WriteAsync("v1.1.0", "- Fixed a crash");

        string content = await File.ReadAllTextAsync(Path_);
        Assert.Equal(
            "# Changelog\n\n## v1.1.0\n\n- Fixed a crash\n\n## v1.0.0\n\n- Added search\n",
            content);
    }

    [Fact]
    public async Task Running_twice_for_the_same_release_does_not_duplicate_the_section()
    {
        FileChangelogMarkdownWriter writer = new(_directory);

        await writer.WriteAsync("v1.0.0", "- Added search");
        await writer.WriteAsync("v1.0.0", "- Added search");

        string content = await File.ReadAllTextAsync(Path_);
        Assert.Equal("# Changelog\n\n## v1.0.0\n\n- Added search\n", content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
