using Chartula.Core.Observability;

namespace Chartula.Core.Tests.Observability;

public sealed class RunMetricsTests
{
    [Fact]
    public void Token_usage_accumulates_per_operation_and_in_total()
    {
        RunMetrics metrics = new();

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 20));
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(150, 30));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(200, 10));

        RunReport report = metrics.Snapshot();

        Assert.Equal(2, report.UsageOf(LlmOperation.Rephrase).TotalCalls);
        Assert.Equal(new TokenUsage(250, 50), report.UsageOf(LlmOperation.Rephrase).Tokens);
        Assert.Equal(1, report.UsageOf(LlmOperation.FaithfulnessCheck).TotalCalls);
        Assert.Equal(new TokenUsage(200, 10), report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens);
        Assert.Equal(510, report.TotalTokens.TotalTokens);
    }

    [Fact]
    public void A_call_the_provider_reported_nothing_for_still_counts_as_a_call()
    {
        RunMetrics metrics = new();

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(null, null));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.TotalCalls);
        Assert.Equal(1, usage.CallsWithoutUsage);
        Assert.Equal(TokenUsage.None, usage.Tokens);
    }

    [Fact]
    public void A_call_that_reports_only_one_side_counts_as_a_call_without_usage()
    {
        RunMetrics metrics = new();

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, null));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(null, 20));

        RunReport report = metrics.Snapshot();

        // A call missing input or output usage counts as a call without usage.
        // The reported side is still added to the tokens.
        Assert.Equal(1, report.UsageOf(LlmOperation.Rephrase).CallsWithoutUsage);
        Assert.Equal(new TokenUsage(100, 0), report.UsageOf(LlmOperation.Rephrase).Tokens);
        Assert.Equal(1, report.UsageOf(LlmOperation.FaithfulnessCheck).CallsWithoutUsage);
        Assert.Equal(new TokenUsage(0, 20), report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens);
    }

    [Fact]
    public void Calls_with_and_without_reported_usage_are_counted_side_by_side()
    {
        RunMetrics metrics = new();

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 20));
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(null, null));
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(150, 30));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);

        // 250 in / 50 out over three calls, one of them without reported usage. Without
        // that count, the total would look cheaper than the run actually was.
        Assert.Equal(3, usage.TotalCalls);
        Assert.Equal(1, usage.CallsWithoutUsage);
        Assert.Equal(new TokenUsage(250, 50), usage.Tokens);
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

        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["a"], thoroughFlags: [], thoroughEvaluated: true);
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: ["b", "c"], thoroughEvaluated: true);
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: [], thoroughEvaluated: true);

        RunReport report = metrics.Snapshot();

        Assert.Equal(new CheckActivity(Runs: 3, RunsWithFindings: 1, Flags: 1), report.RuleBased);
        Assert.Equal(new CheckActivity(Runs: 3, RunsWithFindings: 1, Flags: 2), report.Thorough);
    }

    // "No findings" and "could not be read" both produce an empty flag list. Only the
    // counter tells them apart, so the run summary can too.
    [Fact]
    public void A_thorough_check_that_could_not_be_read_is_counted_apart_from_a_clean_one()
    {
        RunMetrics metrics = new();

        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: [], thoroughEvaluated: true);
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: [], thoroughFlags: [], thoroughEvaluated: false);

        RunReport report = metrics.Snapshot();

        Assert.Equal(2, report.Thorough.Runs);
        Assert.Equal(0, report.Thorough.RunsWithFindings);
        Assert.Equal(1, report.ThoroughNotEvaluated);
    }

    [Fact]
    public void Only_the_flags_the_rule_based_check_missed_count_as_thorough_only()
    {
        RunMetrics metrics = new();

        // "shared" is found by both, so the thorough check added only "extra".
        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["shared"], thoroughFlags: ["shared", "extra"], thoroughEvaluated: true);

        RunReport report = metrics.Snapshot();

        Assert.Equal(2, report.Thorough.Flags);
        Assert.Equal(1, report.ThoroughOnlyFlags);
    }

    [Fact]
    public void A_thorough_check_that_adds_nothing_over_the_free_check_shows_it()
    {
        RunMetrics metrics = new();

        metrics.RecordFaithfulnessChecks(ruleBasedFlags: ["a", "b"], thoroughFlags: ["a", "b"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(900, 40));

        RunReport report = metrics.Snapshot();

        // Nothing gained for 940 tokens: exactly the judgement the report must allow.
        Assert.Equal(0, report.ThoroughOnlyFlags);
        Assert.Equal(940, report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens.TotalTokens);
        Assert.Equal(0, report.UsageOf(LlmOperation.FaithfulnessCheck).CallsWithoutUsage);
    }

    [Fact]
    public void A_snapshot_does_not_change_when_recording_continues()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 5));

        RunReport taken = metrics.Snapshot();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_000, 500));

        Assert.Equal(15, taken.TotalTokens.TotalTokens);
    }

    [Fact]
    public void Recording_from_many_threads_loses_nothing()
    {
        RunMetrics metrics = new();

        Parallel.For(0, 200, i =>
        {
            // Every second call reports no usage, so both counters are under contention.
            long? tokens = i % 2 == 0 ? 1 : null;
            metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(tokens, tokens));
            metrics.RecordFaithfulnessChecks(["a"], ["a", "b"], thoroughEvaluated: true);
        });

        RunReport report = metrics.Snapshot();

        Assert.Equal(200, report.UsageOf(LlmOperation.Rephrase).TotalCalls);
        Assert.Equal(100, report.UsageOf(LlmOperation.Rephrase).CallsWithoutUsage);
        Assert.Equal(200, report.TotalTokens.TotalTokens);
        Assert.Equal(200, report.Thorough.Runs);
        Assert.Equal(200, report.ThoroughOnlyFlags);
    }

    [Fact]
    public void The_null_sink_records_nothing_and_reports_empty()
    {
        IRunMetrics metrics = NullRunMetrics.Instance;

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 100));
        metrics.RecordFaithfulnessChecks(["a"], ["b"], thoroughEvaluated: true);

        Assert.Equal(RunReport.Empty, metrics.Snapshot());
    }

    // #128: time adds up per operation, the longest call is kept, and retries count
    // only from calls whose requests could be counted.
    [Fact]
    public void Durations_add_up_and_the_longest_call_is_kept()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 1) { Duration = TimeSpan.FromSeconds(3), Attempts = 1 });
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 1) { Duration = TimeSpan.FromSeconds(7), Attempts = 3 });
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 1) { Duration = TimeSpan.FromSeconds(1) });

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(TimeSpan.FromSeconds(11), usage.Duration);
        Assert.Equal(TimeSpan.FromSeconds(7), usage.LongestCall);
        Assert.Equal(2, usage.Retries);
    }

    [Fact]
    public void Retries_stay_unknown_when_no_call_could_count_its_requests()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(10, 1));

        Assert.Null(metrics.Snapshot().UsageOf(LlmOperation.Rephrase).Retries);
    }

    [Fact]
    public void The_run_duration_is_reported_once_recorded()
    {
        RunMetrics metrics = new();
        Assert.Null(metrics.Snapshot().Duration);

        metrics.RecordRunDuration(TimeSpan.FromSeconds(64));

        Assert.Equal(TimeSpan.FromSeconds(64), metrics.Snapshot().Duration);
    }

    // null until a call reports a value. A later call without a value leaves the known
    // sum as it is instead of turning it back into null.
    [Fact]
    public void Cached_and_reasoning_tokens_sum_over_the_calls_that_reported_them()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 10));
        Assert.Null(metrics.Snapshot().UsageOf(LlmOperation.Rephrase).ReasoningTokens);

        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 10) { CachedInputTokens = 60, ReasoningTokens = 4 });
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(100, 10) { CachedInputTokens = 40 });

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(100, usage.CachedInputTokens);
        Assert.Equal(4, usage.ReasoningTokens);
    }
}
