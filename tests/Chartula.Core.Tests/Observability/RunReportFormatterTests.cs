using Chartula.Core.Observability;

namespace Chartula.Core.Tests.Observability;

public sealed class RunReportFormatterTests
{
    private static RunReport Report()
    {
        RunMetrics metrics = new();
        metrics.RecordFaithfulnessChecks(["shared"], ["shared", "only thorough"]);
        metrics.RecordLlmCall(LlmOperation.Rephrase, new TokenUsage(1_500, 300));
        metrics.RecordLlmCall(LlmOperation.FaithfulnessCheck, new TokenUsage(2_000, 40));
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

    [Fact]
    public void An_empty_run_formats_without_blowing_up()
    {
        string text = RunReportFormatter.Format(RunReport.Empty);

        Assert.Contains("0 runs", text);
        Assert.Contains("0 tokens", text);
    }
}
