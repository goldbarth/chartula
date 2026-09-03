using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;

namespace Chartula.Infrastructure.Tests.Serialization;

public sealed class FileCustomerPageWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "chartula-page-" + Guid.NewGuid().ToString("N"));

    private static CustomerPage Page(string tag = "v1.0.0")
        => new(tag, new DateOnly(2026, 7, 17), "A release about finding things.", [], "- **Search:** You can find things.");

    [Fact]
    public async Task Writes_one_file_per_release_named_after_its_tag()
    {
        FileCustomerPageWriter writer = new(_directory);

        string path = await writer.WriteAsync(Page());

        Assert.Equal(Path.Combine(_directory, "release-v1.0.0.md"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task The_file_opens_on_the_front_matter_the_format_specifies()
    {
        FileCustomerPageWriter writer = new(_directory);

        string path = await writer.WriteAsync(Page());

        string[] lines = (await File.ReadAllTextAsync(path)).Split('\n');
        Assert.Equal("---", lines[0]);
        Assert.Equal("title: Release 1.0.0", lines[1]);
        Assert.Equal("description: A release about finding things.", lines[2]);
        Assert.Equal("publishedAt: 2026-07-17", lines[3]);
        Assert.Equal("---", lines[4]);
        Assert.Equal(string.Empty, lines[5]);
        Assert.Equal("- **Search:** You can find things.", lines[6]);
    }

    [Fact]
    public async Task Two_releases_do_not_overwrite_each_other()
    {
        FileCustomerPageWriter writer = new(_directory);

        await writer.WriteAsync(Page("v1.0.0"));
        await writer.WriteAsync(Page("v1.1.0"));

        Assert.True(File.Exists(Path.Combine(_directory, "release-v1.0.0.md")));
        Assert.True(File.Exists(Path.Combine(_directory, "release-v1.1.0.md")));
    }

    [Fact]
    public async Task Rewriting_the_same_release_replaces_the_file_rather_than_adding_one()
    {
        FileCustomerPageWriter writer = new(_directory);

        await writer.WriteAsync(Page());
        await writer.WriteAsync(Page() with { Body = "- **Search:** Now with filters." });

        string page = Assert.Single(Directory.GetFiles(_directory));
        Assert.Contains("Now with filters.", await File.ReadAllTextAsync(page), StringComparison.Ordinal);
    }

    [Fact]
    public void A_tag_a_file_system_cannot_carry_still_names_a_file()
    {
        // "release/1.0" is a legal git tag and an illegal file name.
        Assert.Equal("release-release-1.0.md", FileCustomerPageWriter.FileNameFor("release/1.0"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
