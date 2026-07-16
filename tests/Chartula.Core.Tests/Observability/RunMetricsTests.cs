using Chartula.Core.Observability;

namespace Chartula.Core.Tests.Observability;

public sealed class RunMetricsTests
{
    [Fact]
    public void Token_usage_accumulates_per_operation_and_in_total()
    {
        RunMetrics metrics = new();

        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(100, 20));
        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(150, 30));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new TokenUsage(200, 10));

        RunReport report = metrics.Snapshot();

        Assert.Equal(2, report.UsageOf(LlmOperation.Rephrase).Calls);
        Assert.Equal(new TokenUsage(250, 50), report.UsageOf(LlmOperation.Rephrase).Tokens);
        Assert.Equal(1, report.UsageOf(LlmOperation.FaithfulnessCheck).Calls);
        Assert.Equal(new TokenUsage(200, 10), report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens);
        Assert.Equal(510, report.TotalTokens.TotalTokens);
    }

    [Fact]
    public void An_operation_that_never_ran_reports_no_usage()
    {
        RunReport report = new RunMetrics().Snapshot();

        Assert.Equal(LlmUsage.None, report.UsageOf(LlmOperation.Rephrase));
        Assert.Equal(0, report.TotalTokens.TotalTokens);
    }

    [Fact]
    public void Each_check_counts_its_runs_the_runs_that_fired_and_its_flags()
    {
        RunMetrics metrics = new();

        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["a"], thoroughFlags: []);
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: ["b", "c"]);
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: []);

        RunReport report = metrics.Snapshot();

        Assert.Equal(new CheckActivity(Runs: 3, RunsWithFindings: 1, Flags: 1), report.RuleBased);
        Assert.Equal(new CheckActivity(Runs: 3, RunsWithFindings: 1, Flags: 2), report.Thorough);
    }

    [Fact]
    public void Only_the_flags_the_rule_based_check_missed_count_as_thorough_only()
    {
        RunMetrics metrics = new();

        // "shared" is found by both, so the thorough check added only "extra".
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["shared"], thoroughFlags: ["shared", "extra"]);

        RunReport report = metrics.Snapshot();

        Assert.Equal(2, report.Thorough.Flags);
        Assert.Equal(1, report.ThoroughOnlyFlags);
    }

    [Fact]
    public void A_thorough_check_that_adds_nothing_over_the_free_check_shows_it()
    {
        RunMetrics metrics = new();

        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["a", "b"], thoroughFlags: ["a", "b"]);
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new TokenUsage(900, 40));

        RunReport report = metrics.Snapshot();

        // Nothing gained, 940 tokens spent - exactly the judgement the report must allow.
        Assert.Equal(0, report.ThoroughOnlyFlags);
        Assert.Equal(940, report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens.TotalTokens);
    }

    [Fact]
    public void A_snapshot_does_not_change_when_recording_continues()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(10, 5));

        RunReport taken = metrics.Snapshot();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(1_000, 500));

        Assert.Equal(15, taken.TotalTokens.TotalTokens);
    }

    [Fact]
    public void Recording_from_many_threads_loses_nothing()
    {
        RunMetrics metrics = new();

        Parallel.For(0, 200, _ =>
        {
            metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(1, 1));
            metrics.RecordFaithfulnessChecks(["a"], ["a", "b"]);
        });

        RunReport report = metrics.Snapshot();

        Assert.Equal(200, report.UsageOf(LlmOperation.Rephrase).Calls);
        Assert.Equal(400, report.TotalTokens.TotalTokens);
        Assert.Equal(200, report.Thorough.Runs);
        Assert.Equal(200, report.ThoroughOnlyFlags);
    }

    [Fact]
    public void The_null_sink_records_nothing_and_reports_empty()
    {
        IRunMetrics metrics = NullRunMetrics.Instance;

        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(100, 100));
        metrics.RecordFaithfulnessChecks(["a"], ["b"]);

        Assert.Equal(RunReport.Empty, metrics.Snapshot());
    }
}
