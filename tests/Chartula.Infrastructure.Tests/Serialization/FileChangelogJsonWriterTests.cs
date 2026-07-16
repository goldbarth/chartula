using System.Text.Json;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;

namespace Chartula.Infrastructure.Tests.Serialization;

public sealed class FileChangelogJsonWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "chartula-json-" + Guid.NewGuid().ToString("N"));

    private static FactBase Sample() => new("v1.0.0", [
        new ChangeFact("feat: add search", 7, "https://example/pull/7",
            ChangeCategory.Feature, IsUserVisible: true, IsBreaking: false, [], "Adds search."),
    ]);

    [Fact]
    public async Task Writes_changelog_json_into_the_output_directory()
    {
        FileChangelogJsonWriter writer = new(_directory);

        string path = await writer.WriteAsync(Sample());

        Assert.Equal(Path.Combine(_directory, "changelog.json"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Writes_valid_parseable_json_matching_the_documented_format()
    {
        string path = await new FileChangelogJsonWriter(_directory).WriteAsync(Sample());

        string json = await File.ReadAllTextAsync(path);
        using JsonDocument parsed = JsonDocument.Parse(json); // valid JSON

        JsonElement root = parsed.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("v1.0.0", root.GetProperty("tag").GetString());
        Assert.Equal("Feature", root.GetProperty("changes")[0].GetProperty("category").GetString());
    }

    [Fact]
    public async Task The_written_file_round_trips_through_the_serializer()
    {
        string path = await new FileChangelogJsonWriter(_directory).WriteAsync(Sample());

        ChangelogDocument document = ChangelogJsonSerializer.Deserialize(await File.ReadAllTextAsync(path));

        Assert.Equal("v1.0.0", document.Tag);
        Assert.Equal("feat: add search", Assert.Single(document.Changes).Title);
    }

    [Fact]
    public async Task Creates_the_output_directory_if_it_does_not_exist()
    {
        string nested = Path.Combine(_directory, "nested", "out");

        string path = await new FileChangelogJsonWriter(nested).WriteAsync(Sample());

        Assert.True(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
