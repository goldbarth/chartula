namespace Chartula.Core.Observability;

/// <summary>What one LLM operation cost across a run.</summary>
/// <param name="TotalCalls">How many calls were made.</param>
/// <param name="CallsWithoutUsage">How many calls were made without usage reported.</param>
/// <param name="Tokens">The tokens those calls consumed.</param>
public sealed record LlmUsage(int TotalCalls, int CallsWithoutUsage, TokenUsage Tokens)
{
    /// <summary>Nothing called, nothing spent.</summary>
    public static LlmUsage None { get; } = new(0, 0, TokenUsage.None);

    /// <summary>
    /// Input tokens served from the provider's cache, summed over the calls that
    /// reported it. <c>null</c> when no call reported it.
    /// </summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>
    /// Output tokens spent reasoning, summed over the calls that reported it.
    /// <c>null</c> when no call reported it, which does not mean there was no reasoning.
    /// </summary>
    public long? ReasoningTokens { get; init; }

    /// <summary>Calls that ended in an error. Not in <see cref="TotalCalls"/>, which counts answers.</summary>
    public int FailedCalls { get; init; }

    /// <summary>The time all calls took, failed ones included.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The longest single call: the one to look at when a run was slow.</summary>
    public TimeSpan LongestCall { get; init; }

    /// <summary>
    /// Requests sent beyond the first, across the calls whose requests could be counted.
    /// <c>null</c> when no call could be counted, which does not mean there were no retries.
    /// </summary>
    public int? Retries { get; init; }
}

/// <summary>How often a faithfulness check ran and how often it found something.</summary>
/// <param name="Runs">How many times the check ran.</param>
/// <param name="RunsWithFindings">How many of those runs flagged at least one claim.</param>
/// <param name="Flags">How many claims the check flagged in total.</param>
public sealed record CheckActivity(int Runs, int RunsWithFindings, int Flags);

/// <summary>
/// What a run did and what it cost.
/// The thorough check is worth its cost when <see cref="ThoroughOnlyFlags"/>, the
/// claims only it caught, justifies the tokens of <see cref="LlmOperation.FaithfulnessCheck"/>.
/// </summary>
/// <param name="RuleBased">Activity of the rule-based check, which costs no tokens.</param>
/// <param name="Thorough">
/// Activity of the thorough check.
/// <see cref="CheckActivity.Runs"/> counts how often the check was requested.
/// The calls under <see cref="LlmOperation.FaithfulnessCheck"/> count how often it
/// actually reached the model.
/// Runs without calls mean the check was turned off or had nothing to check.
/// </param>
/// <param name="ThoroughNotEvaluated">
/// Thorough runs that reached the model and came back unreadable.
/// These runs verified nothing, so they are counted separately from runs that found no claims.
/// </param>
/// <param name="ThoroughOnlyFlags">
/// Claims flagged by the thorough check that the rule-based check missed. This is the
/// value the thorough check adds over the free check.
/// </param>
/// <param name="Llm">Calls and tokens per operation.</param>
public sealed record RunReport(
    CheckActivity RuleBased,
    CheckActivity Thorough,
    int ThoroughNotEvaluated,
    int ThoroughOnlyFlags,
    IReadOnlyDictionary<LlmOperation, LlmUsage> Llm)
{
    /// <summary>How much release the run worked on, or <c>null</c> when it was not recorded.</summary>
    public ReleaseScope? Scope { get; init; }

    /// <summary>How long the run took, or <c>null</c> when it was not measured.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>An empty report: nothing ran.</summary>
    public static RunReport Empty { get; } = new(
        new CheckActivity(0, 0, 0),
        new CheckActivity(0, 0, 0),
        0,
        0,
        new Dictionary<LlmOperation, LlmUsage>());

    /// <summary>Tokens consumed by the whole run.</summary>
    public TokenUsage TotalTokens => Llm.Values.Aggregate(TokenUsage.None, static (sum, usage) => sum + usage.Tokens);

    /// <summary>Total calls made to the LLM.</summary>
    public int TotalCalls => Llm.Values.Sum(usage => usage.TotalCalls);

    /// <summary>Calls whose usage the provider did not fully report. If any, the token total is only a lower bound.</summary>
    public int CallsWithoutUsage => Llm.Values.Sum(usage => usage.CallsWithoutUsage);

    /// <summary>Calls that ended in an error, across operations.</summary>
    public int FailedCalls => Llm.Values.Sum(usage => usage.FailedCalls);

    /// <summary>Calls and tokens for one operation; none if it never ran.</summary>
    public LlmUsage UsageOf(LlmOperation operation)
        => Llm.TryGetValue(operation, out LlmUsage? usage) ? usage : LlmUsage.None;
}
