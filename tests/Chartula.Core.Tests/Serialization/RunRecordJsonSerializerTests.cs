using System.Text.Json;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

public sealed class RunRecordJsonSerializerTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 30, 15, 789, TimeSpan.FromHours(2));

    private static RunRecord Record(PipelineMode mode = PipelineMode.Generate)
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, 1_500, 300);
        metrics.RecordLlmCall(LlmOperation.Rephrase, null, 200);
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);

        return new RunRecord(
            "v1.0.0",
            new RepositoryCoordinates("octo", "repo"),
            mode,
            [
                new AudienceOutcome(Audience.Technical, Success: true, "- Added search", ["shared"], Error: null),
                new AudienceOutcome(Audience.Customer, Success: false, Text: null, [], "Status Code: Unauthorized"),
            ],
            metrics.Snapshot());
    }

    [Fact]
    public void Writes_the_metrics_as_the_summary_prints_them()
    {
        RunRecordMetrics metrics = RunRecordJsonSerializer.Deserialize(
            RunRecordJsonSerializer.Serialize(Record(), At)).Metrics;

        Assert.Equal(new RunRecordLlmUsage(2, 1, 1_500, 500), metrics.Rephrase);
        Assert.Equal(new RunRecordCheck(1, 1, 1), metrics.RuleBasedCheck);
        Assert.Equal(new RunRecordThoroughCheck(1, 1, 2, 1, 0), metrics.ThoroughCheck);
    }

    // An operation that never ran is a zero, not an absence: "no thorough check
    // calls" is what a run with the check off looks like, and it has to compare.
    [Fact]
    public void An_operation_without_calls_is_written_as_zero()
    {
        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(Record(), At));

        JsonElement check = parsed.RootElement.GetProperty("metrics").GetProperty("faithfulnessCheck");
        Assert.Equal(0, check.GetProperty("calls").GetInt32());
        Assert.Equal(0, check.GetProperty("inputTokens").GetInt64());
    }

    [Fact]
    public void A_rendered_audience_has_flags_and_a_failed_one_its_error()
    {
        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(Record(), At));

        JsonElement technical = parsed.RootElement.GetProperty("audiences")[0];
        Assert.Equal("technical", technical.GetProperty("audience").GetString());
        Assert.True(technical.GetProperty("rendered").GetBoolean());
        Assert.Equal("shared", technical.GetProperty("flags")[0].GetString());
        Assert.False(technical.TryGetProperty("error", out _));

        // A failed audience was never checked, so it has no flags, not an empty list.
        JsonElement customer = parsed.RootElement.GetProperty("audiences")[1];
        Assert.False(customer.GetProperty("rendered").GetBoolean());
        Assert.Equal("Status Code: Unauthorized", customer.GetProperty("error").GetString());
        Assert.False(customer.TryGetProperty("flags", out _));
    }

    [Fact]
    public void Records_when_where_and_how_the_run_was_made()
    {
        RunRecordDocument document = RunRecordJsonSerializer.Deserialize(
            RunRecordJsonSerializer.Serialize(Record(PipelineMode.GenerateWithoutPublishing), At));

        Assert.Equal(1, document.SchemaVersion);
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 10, 30, 15, TimeSpan.Zero), document.RecordedAt);
        Assert.Equal(TimeSpan.Zero, document.RecordedAt.Offset);
        Assert.Equal("v1.0.0", document.Tag);
        Assert.Equal("octo/repo", document.Repository);
        Assert.Equal("generate --no-publish", document.Mode);
    }

    [Fact]
    public void Carries_the_provenance_in_the_fields_changelog_json_uses()
    {
        RunProvenance provenance = new("0.1.0", "anthropic", "claude-sonnet-5", "sha256:ff", "disabled", false, "title-only");

        RunRecordDocument document = RunRecordJsonSerializer.Deserialize(
            RunRecordJsonSerializer.Serialize(Record(), At, provenance));

        Assert.Equal(
            new ChangelogProvenance("0.1.0", "anthropic", "claude-sonnet-5", "sha256:ff", "disabled", false, "title-only"),
            document.Provenance);
    }

    [Fact]
    public void Without_provenance_the_field_is_left_out()
    {
        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(Record(), At));

        Assert.False(parsed.RootElement.TryGetProperty("provenance", out _));
    }
}
