using Chartula.Core.Observability;

namespace Chartula.Core.Tests.Observability;

public sealed class RunReportFormatterTests
{
    private static RunReport Report()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, 1_500, 300);
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, 2_000, 40);
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

    // One call the provider accounted for, one it did not - the run whose token total
    // cannot be taken at face value.
    private static RunReport ReportWithAnUnreportedCall()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"], thoroughEvaluated: true);
        metrics.RecordLlmCall(LlmOperation.Rephrase, 1_500, 300);
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, null, null);
        return metrics.Snapshot();
    }

    [Fact]
    public void The_summary_marks_the_total_as_a_lower_bound_when_a_call_went_unreported()
    {
        string text = RunReportFormatter.Format(ReportWithAnUnreportedCall());

        // Without this line the 1,800 read as the run's cost, when they are only the part
        // a provider happened to account for.
        Assert.Contains("Total:            1,800 tokens", text);
        Assert.Contains("    lower bound, 1 of 2 calls unreported", text);
    }

    [Fact]
    public void The_summary_stays_silent_about_unreported_calls_when_every_call_reported()
    {
        string text = RunReportFormatter.Format(Report());

        // A total that is exact must not be hedged - the note is a signal, not a disclaimer.
        Assert.DoesNotContain("lower bound", text);
    }

    [Fact]
    public void An_empty_run_formats_without_blowing_up()
    {
        string text = RunReportFormatter.Format(RunReport.Empty);

        Assert.Contains("0 runs", text);
        Assert.Contains("0 tokens", text);
    }
}
