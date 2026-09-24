using Chartula.Core.Observability;

namespace Chartula.Core.Tests.Observability;

public sealed class RunReportFormatterTests
{
    private static RunReport Report()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(2_000, 40));
        return metrics.Snapshot();
    }

    [Fact]
    public void The_summary_puts_the_thorough_check_added_value_next_to_its_token_cost()
    {
        string text = RunReportFormatter.Format(Report());

        // The judgement the report exists for: 1 claim gained for 2,040 tokens.
        Assert.Contains("caught 1 claim the rule-based check missed, for 2,040 tokens in 1 call", text);
    }

    [Fact]
    public void The_summary_reports_check_runs_flags_and_the_total()
    {
        string text = RunReportFormatter.Format(Report());

        Assert.Contains("Rule-based check: 1 run, 1 with findings, 1 claim, no tokens", text);
        Assert.Contains("Thorough check:   1 run, 1 with findings, 2 claims", text);
        Assert.Contains("3,840 tokens", text);
    }

    // One call with reported usage and one without: a run whose token total cannot be
    // taken at face value.
    private static RunReport ReportWithAnUnreportedCall()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(null, null));
        return metrics.Snapshot();
    }

    [Fact]
    public void The_summary_marks_the_total_as_a_lower_bound_when_a_call_went_unreported()
    {
        string text = RunReportFormatter.Format(ReportWithAnUnreportedCall());

        // Without the "lower bound" line, 1,800 would read as the run's cost, although it
        // is only the reported part.
        Assert.Contains("Total:            1,800 tokens", text);
        Assert.Contains("    lower bound, 1 of 2 calls unreported", text);
    }

    [Fact]
    public void The_summary_stays_silent_about_unreported_calls_when_every_call_reported()
    {
        string text = RunReportFormatter.Format(Report());

        // An exact total gets no "lower bound" note: the note is a signal, not a disclaimer.
        Assert.DoesNotContain("lower bound", text);
    }

    [Fact]
    public void An_empty_run_formats_without_blowing_up()
    {
        string text = RunReportFormatter.Format(RunReport.Empty);

        Assert.Contains("0 runs", text);
        Assert.Contains("0 tokens", text);
    }

    // #128: which call took the time, whether it was retried, and how long the run was.
    [Fact]
    public void The_summary_reports_time_per_operation_the_run_and_retries()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks([], [], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300) { Duration = TimeSpan.FromSeconds(18.95), Attempts = 1 });
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300) { Duration = TimeSpan.FromSeconds(22.1), Attempts = 2 });
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new LlmCall(2_000, 40) { Duration = TimeSpan.FromSeconds(6.5), Attempts = 1 });
        metrics.RecordRunDuration(TimeSpan.FromSeconds(64));

        string text = RunReportFormatter.Format(metrics.Snapshot());

        Assert.Contains("  Rephrasing:       2 calls, 3,000 in / 600 out, 41.1 s (longest 22.1 s)", text);
        Assert.Contains("2,000 in / 40 out, 6.5 s", text);
        Assert.Contains("  Total:            5,640 tokens in 1 min 4 s", text);
        Assert.Contains("  Retries:          1 (rephrasing 1, thorough check 0)", text);
    }

    [Fact]
    public void Retries_that_could_not_be_counted_are_said_to_be_not_observed()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300));

        Assert.Contains("  Retries:          not observed", RunReportFormatter.Format(metrics.Snapshot()));
    }

    [Fact]
    public void Calls_answered_first_time_read_as_no_retries()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(1_500, 300) { Attempts = 1 });

        Assert.Contains("  Retries:          none", RunReportFormatter.Format(metrics.Snapshot()));
    }

    [Fact]
    public void Failed_calls_are_named_under_their_operation()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(null, null) { Duration = TimeSpan.FromMinutes(3), Attempts = 4, Failed = true });

        string text = RunReportFormatter.Format(metrics.Snapshot());

        Assert.Contains("  Rephrasing:       0 calls, 0 in / 0 out, 3 min 0 s", text);
        Assert.Contains("    1 call failed without an answer", text);
        Assert.Contains("  Retries:          3 (rephrasing 3)", text);
        Assert.DoesNotContain("lower bound", text);
    }

    [Fact]
    public void The_summary_breaks_out_cached_input_and_reasoning()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(5_000, 300) { CachedInputTokens = 4_000, ReasoningTokens = 250 });

        Assert.Contains("    of which 4,000 in cached, 250 out reasoning", RunReportFormatter.Format(metrics.Snapshot()));
    }

    // A provider that includes reasoning in the output without a separate count.
    // Printing zero would claim the model did not reason.
    [Fact]
    public void What_the_provider_does_not_break_out_is_said_to_be_not_reported()
    {
        RunMetrics metrics = new();
        metrics.RecordLlmCall(LlmOperation.Rephrase, new LlmCall(5_000, 300) { CachedInputTokens = 0 });

        Assert.Contains("    of which 0 in cached, reasoning not reported", RunReportFormatter.Format(metrics.Snapshot()));
    }

    [Fact]
    public void The_summary_opens_with_how_much_release_the_run_worked_on()
    {
        RunMetrics metrics = new();
        metrics.RecordReleaseScope(new ReleaseScope(14, 10, 9, 7, 22_512));

        Assert.Contains(
            "  Release:          14 commits, 10 pull requests, 9 facts (7 with a description, 22,512 characters)",
            RunReportFormatter.Format(metrics.Snapshot()));
    }

    [Fact]
    public void A_report_without_a_scope_has_no_release_line()
    {
        Assert.DoesNotContain("Release:", RunReportFormatter.Format(RunReport.Empty));
    }
}
