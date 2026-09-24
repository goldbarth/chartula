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
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300));
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(null, 200));
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);

        return new RunRecord(
            "v1.0.0",
            new RepositoryCoordinates("octo", "repo"),
            mode,
            [
                new AudienceOutcome(Audience.Technical, Success: true, "- Added search", [new FaithfulnessFlag("shared"), new FaithfulnessFlag("overstated", 12)], Error: null),
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

    // An operation that never ran is written as zero, not left out. "No thorough check
    // calls" is what a run with the check off looks like, and it must be comparable.
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
        Assert.Equal(2, technical.GetProperty("flags").GetArrayLength());
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

        Assert.Equal(2, document.SchemaVersion);
        Assert.Equal(new DateTimeOffset(2026, 9, 22, 10, 30, 15, TimeSpan.Zero), document.RecordedAt);
        Assert.Equal(TimeSpan.Zero, document.RecordedAt.Offset);
        Assert.Equal("v1.0.0", document.Tag);
        Assert.Equal("octo/repo", document.Repository);
        Assert.Equal("generate --no-publish", document.Mode);
    }

    // Flags from many runs group by the fact they concern, without parsing their text.
    [Fact]
    public void Each_flag_names_its_pull_request_and_one_about_no_fact_names_none()
    {
        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(Record(), At));
        JsonElement flags = parsed.RootElement.GetProperty("audiences")[0].GetProperty("flags");

        Assert.Equal("shared", flags[0].GetProperty("text").GetString());
        Assert.False(flags[0].TryGetProperty("pullRequest", out _));
        Assert.Equal("overstated", flags[1].GetProperty("text").GetString());
        Assert.Equal(12, flags[1].GetProperty("pullRequest").GetInt32());
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

    // #128: the duration of each operation and of the run, and the retries where they
    // could be counted.
    [Fact]
    public void Writes_time_failed_calls_and_retries()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 1) { Duration = TimeSpan.FromMilliseconds(1_234.5678), Attempts = 2 });
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(null, null) { Duration = TimeSpan.FromSeconds(2), Attempts = 4, Failed = true });
        metrics.RecordRunDuration(TimeSpan.FromSeconds(64.5));
        RunRecord record = Record() with { Metrics = metrics.Snapshot() };

        RunRecordMetrics written = RunRecordJsonSerializer.Deserialize(RunRecordJsonSerializer.Serialize(record, At)).Metrics;

        Assert.Equal(new RunRecordLlmUsage(1, 0, 10, 1, 1, 3.235, 2, 4), written.Rephrase);
        Assert.Equal(64.5, written.DurationSeconds);
    }

    // Unknown values are left out, so a record never claims a zero count it did not observe.
    [Fact]
    public void Retries_that_were_not_observed_are_left_out()
    {
        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(Record(), At));

        Assert.False(parsed.RootElement.GetProperty("metrics").GetProperty("rephrase").TryGetProperty("retries", out _));
    }

    // Records written before the fields existed still read.
    [Fact]
    public void A_record_without_time_fields_reads_with_zeros()
    {
        const string earlier = """
            {"schemaVersion":1,"recordedAt":"2026-09-22T12:30:15+00:00","tag":"v1","repository":"o/r","mode":"generate",
             "audiences":[],"metrics":{
               "rephrase":{"calls":1,"callsWithoutUsage":0,"inputTokens":5,"outputTokens":1},
               "faithfulnessCheck":{"calls":0,"callsWithoutUsage":0,"inputTokens":0,"outputTokens":0},
               "ruleBasedCheck":{"runs":0,"runsWithFindings":0,"flags":0},
               "thoroughCheck":{"runs":0,"runsWithFindings":0,"flags":0,"onlyThoroughFlags":0,"notEvaluated":0}}}
            """;

        RunRecordMetrics metrics = RunRecordJsonSerializer.Deserialize(earlier).Metrics;

        Assert.Equal(new RunRecordLlmUsage(1, 0, 5, 1), metrics.Rephrase);
        Assert.Null(metrics.DurationSeconds);
    }

    [Fact]
    public void Writes_cached_and_reasoning_tokens_and_leaves_out_what_was_not_reported()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(5_000, 300) { CachedInputTokens = 4_000 });
        RunRecord record = Record() with { Metrics = metrics.Snapshot() };

        using JsonDocument parsed = JsonDocument.Parse(RunRecordJsonSerializer.Serialize(record, At));
        JsonElement rephrase = parsed.RootElement.GetProperty("metrics").GetProperty("rephrase");

        Assert.Equal(4_000, rephrase.GetProperty("cachedInputTokens").GetInt64());
        Assert.False(rephrase.TryGetProperty("reasoningTokens", out _));
    }

    [Fact]
    public void Writes_how_much_release_the_run_worked_on()
    {
        RunMetrics metrics = new();
        metrics.RecordReleaseScope(new ReleaseScope(14, 10, 9, 7, 22_512));
        RunRecord record = Record() with { Metrics = metrics.Snapshot() };

        RunRecordMetrics written = RunRecordJsonSerializer.Deserialize(RunRecordJsonSerializer.Serialize(record, At)).Metrics;

        Assert.Equal(new RunRecordRelease(14, 10, 9, 7, 22_512), written.Release);
    }
}
