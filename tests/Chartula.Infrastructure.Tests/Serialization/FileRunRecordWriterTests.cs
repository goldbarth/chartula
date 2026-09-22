using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;

namespace Chartula.Infrastructure.Tests.Serialization;

public sealed class FileRunRecordWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "chartula-runs-" + Guid.NewGuid().ToString("N"));

    private static readonly FixedTime Noon = new(new DateTimeOffset(2026, 9, 22, 12, 30, 15, TimeSpan.Zero));

    private static RunRecord Record(string tag = "v1.0.0") => new(
        tag,
        new RepositoryCoordinates("octo", "repo"),
        PipelineMode.Generate,
        [new AudienceOutcome(Audience.Technical, Success: true, "- Added search", [], Error: null)],
        RunReport.Empty);

    [Fact]
    public async Task Writes_one_file_per_run_named_by_its_time_and_tag()
    {
        string path = await new FileRunRecordWriter(_directory, timeProvider: Noon).WriteAsync(Record());

        Assert.Equal(Path.Combine(_directory, "chartula-runs", "20260922T123015Z-v1.0.0.json"), path);
        Assert.Equal("v1.0.0", RunRecordJsonSerializer.Deserialize(await File.ReadAllTextAsync(path)).Tag);
    }

    [Fact]
    public async Task A_second_run_in_the_same_second_keeps_the_first()
    {
        FileRunRecordWriter writer = new(_directory, timeProvider: Noon);

        string first = await writer.WriteAsync(Record());
        string second = await writer.WriteAsync(Record());

        Assert.NotEqual(first, second);
        Assert.EndsWith("20260922T123015Z-v1.0.0-2.json", second);
        Assert.True(File.Exists(first));
    }

    [Fact]
    public async Task A_tag_with_a_slash_stays_one_file_name()
    {
        string path = await new FileRunRecordWriter(_directory, timeProvider: Noon).WriteAsync(Record("release/1.2"));

        Assert.Equal("20260922T123015Z-release-1.2.json", Path.GetFileName(path));
    }

    [Fact]
    public async Task The_record_carries_the_provenance_it_was_given()
    {
        string path = await new FileRunRecordWriter(
                _directory, new RunProvenance("0.1.0", "anthropic", "claude-sonnet-5", "sha256:ff"), Noon)
            .WriteAsync(Record());

        Assert.Equal(
            "claude-sonnet-5",
            RunRecordJsonSerializer.Deserialize(await File.ReadAllTextAsync(path)).Provenance?.Model);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
