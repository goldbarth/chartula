using System.Globalization;
using System.Text;

namespace Chartula.Core.Observability;

/// <summary>
/// Renders a <see cref="RunReport"/> as the run summary.
/// It shows the thorough check's added value next to its token cost, so a run's output
/// alone answers whether the thorough check is worth its cost.
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
        if (report.Scope is { } scope)
        {
            builder.AppendLine($"  Release:          {Scope(scope)}");
        }

        builder.AppendLine($"  Rule-based check: {Activity(report.RuleBased)}, no tokens");
        builder.AppendLine($"  Thorough check:   {Activity(report.Thorough)}, {Tokens(check.Tokens)}{Time(check)}");
        AppendTokenDetail(builder, check);
        AppendFailures(builder, check);
        if (report.ThoroughNotEvaluated > 0)
        {
            builder.AppendLine(
                $"    {Count(report.ThoroughNotEvaluated)} of {Runs(report.Thorough.Runs)} came back unreadable "
                + "and verified nothing");
        }

        builder.AppendLine(
            $"    caught {Claims(report.ThoroughOnlyFlags)} the rule-based check missed, "
            + $"for {Count(check.Tokens.TotalTokens)} tokens in {Calls(check.TotalCalls)}");
        builder.AppendLine($"  Rephrasing:       {Calls(rephrase.TotalCalls)}, {Tokens(rephrase.Tokens)}{Time(rephrase)}");
        AppendTokenDetail(builder, rephrase);
        AppendFailures(builder, rephrase);
        string runTime = report.Duration is { } duration ? $" in {Duration(duration)}" : string.Empty;
        builder.AppendLine($"  Total:            {Count(report.TotalTokens.TotalTokens)} tokens{runTime}");
        if (report.CallsWithoutUsage > 0)
        {
            builder.AppendLine($"    lower bound, {report.CallsWithoutUsage} of {report.TotalCalls} calls unreported");
        }

        if (rephrase.TotalCalls + rephrase.FailedCalls + check.TotalCalls + check.FailedCalls > 0)
        {
            builder.AppendLine($"  Retries:          {Retries(rephrase, check)}");
        }

        return builder.ToString();
    }

    // The time an operation's calls took, plus the longest call when there were several.
    // One slow call and uniformly slow calls need different fixes.
    private static string Time(LlmUsage usage)
    {
        int calls = usage.TotalCalls + usage.FailedCalls;
        if (calls == 0)
        {
            return string.Empty;
        }

        return calls == 1
            ? $", {Duration(usage.Duration)}"
            : $", {Duration(usage.Duration)} (longest {Duration(usage.LongestCall)})";
    }

    // Which part of the tokens was cheaper (cached) or invisible (reasoning).
    // Print "not reported" when the provider does not report a part: a zero would
    // claim the model did not reason.
    private static void AppendTokenDetail(StringBuilder builder, LlmUsage usage)
    {
        if (usage.TotalCalls == 0)
        {
            return;
        }

        string cached = usage.CachedInputTokens is { } c ? $"{Count(c)} in cached" : "cached input not reported";
        string reasoning = usage.ReasoningTokens is { } r ? $"{Count(r)} out reasoning" : "reasoning not reported";
        builder.AppendLine($"    of which {cached}, {reasoning}");
    }

    private static void AppendFailures(StringBuilder builder, LlmUsage usage)
    {
        if (usage.FailedCalls > 0)
        {
            builder.AppendLine($"    {Calls(usage.FailedCalls)} failed without an answer");
        }
    }

    // Print "not observed" instead of zero: zero would claim every call succeeded on the first try.
    private static string Retries(LlmUsage rephrase, LlmUsage check)
    {
        if (rephrase.Retries is null && check.Retries is null)
        {
            return "not observed";
        }

        int total = (rephrase.Retries ?? 0) + (check.Retries ?? 0);
        if (total == 0)
        {
            return "none";
        }

        // List only operations that made calls. An operation that never ran has nothing to observe.
        IEnumerable<string> parts = new[] { ("rephrasing", rephrase), ("thorough check", check) }
            .Where(static part => part.Item2.TotalCalls + part.Item2.FailedCalls > 0)
            .Select(static part => $"{part.Item1} {Observed(part.Item2.Retries)}");
        return $"{Count(total)} ({string.Join(", ", parts)})";
    }

    private static string Observed(int? retries) => retries is { } value ? Count(value) : "not observed";

    private static string Duration(TimeSpan duration)
        => duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s"
            : $"{(int)duration.TotalMinutes} min {duration.Seconds} s";

    // The context for every number below it: cost depends more on description length
    // than on the number of pull requests.
    private static string Scope(ReleaseScope scope)
        => $"{Count(scope.Commits)} {(scope.Commits == 1 ? "commit" : "commits")}, "
           + $"{Count(scope.PullRequests)} {(scope.PullRequests == 1 ? "pull request" : "pull requests")}, "
           + $"{Count(scope.Facts)} {(scope.Facts == 1 ? "fact" : "facts")} "
           + $"({Count(scope.FactsWithDescription)} with a description, {Count(scope.DescriptionCharacters)} characters)";

    private static string Activity(CheckActivity activity)
        => $"{Runs(activity.Runs)}, {Count(activity.RunsWithFindings)} with findings, {Claims(activity.Flags)}";

    private static string Tokens(TokenUsage usage)
        => $"{Count(usage.InputTokens)} in / {Count(usage.OutputTokens)} out";

    private static string Runs(int runs) => $"{Count(runs)} {(runs == 1 ? "run" : "runs")}";

    private static string Calls(int calls) => $"{Count(calls)} {(calls == 1 ? "call" : "calls")}";

    private static string Claims(int claims) => $"{Count(claims)} {(claims == 1 ? "claim" : "claims")}";

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
