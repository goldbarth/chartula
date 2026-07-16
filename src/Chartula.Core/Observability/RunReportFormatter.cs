using System.Globalization;
using System.Text;

namespace Chartula.Core.Observability;

/// <summary>
/// Renders a <see cref="RunReport"/> as the run summary. It puts the thorough check's
/// added value next to its token cost, so the question "does the thorough check earn its
/// cost?" can be answered from a run's output alone.
/// </summary>
public static class RunReportFormatter
{
    public static string Format(RunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        LlmUsage rephrase = report.UsageOf(LlmOperation.Rephrase);
        LlmUsage check = report.UsageOf(LlmOperation.FaithfulnessCheck);

        StringBuilder builder = new();
        builder.AppendLine("Run metrics");
        builder.AppendLine($"  Rule-based check: {Activity(report.RuleBased)}, no tokens");
        builder.AppendLine($"  Thorough check:   {Activity(report.Thorough)}, {Tokens(check.Tokens)}");
        builder.AppendLine(
            $"    caught {Claims(report.ThoroughOnlyFlags)} the rule-based check missed, "
            + $"for {Count(check.Tokens.TotalTokens)} tokens in {Calls(check.Calls)}");
        builder.AppendLine($"  Rephrasing:       {Calls(rephrase.Calls)}, {Tokens(rephrase.Tokens)}");
        builder.AppendLine($"  Total:            {Count(report.TotalTokens.TotalTokens)} tokens");
        return builder.ToString();
    }

    private static string Activity(CheckActivity activity)
        => $"{Runs(activity.Runs)}, {Count(activity.RunsWithFindings)} with findings, {Claims(activity.Flags)}";

    private static string Tokens(TokenUsage usage)
        => $"{Count(usage.InputTokens)} in / {Count(usage.OutputTokens)} out";

    private static string Runs(int runs) => $"{Count(runs)} {(runs == 1 ? "run" : "runs")}";

    private static string Calls(int calls) => $"{Count(calls)} {(calls == 1 ? "call" : "calls")}";

    private static string Claims(int claims) => $"{Count(claims)} {(claims == 1 ? "claim" : "claims")}";

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
