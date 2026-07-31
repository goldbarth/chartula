namespace Chartula.Core.Observability;

/// <summary>What one LLM operation cost across a run.</summary>
/// <param name="TotalCalls">How many calls were made.</param>
/// <param name="CallsWithoutUsage">How many calls were made without usage reported.</param>
/// <param name="Tokens">The tokens those calls consumed.</param>
public sealed record LlmUsage(int TotalCalls, int CallsWithoutUsage, TokenUsage Tokens)
{
    /// <summary>Nothing called, nothing spent.</summary>
    public static LlmUsage None { get; } = new(0, 0, TokenUsage.None);
}

/// <summary>How often a faithfulness check ran and how often it found something.</summary>
/// <param name="Runs">How many times the check ran.</param>
/// <param name="RunsWithFindings">How many of those runs flagged at least one claim.</param>
/// <param name="Flags">How many claims the check flagged in total.</param>
public sealed record CheckActivity(int Runs, int RunsWithFindings, int Flags);

/// <summary>
/// What a run did and what it cost. The thorough check earns its cost when
/// <see cref="ThoroughOnlyFlags"/> - the claims only it caught - justifies the tokens
/// attributed to <see cref="LlmOperation.FaithfulnessCheck"/>.
/// </summary>
/// <param name="RuleBased">Activity of the rule-based check, which costs no tokens.</param>
/// <param name="Thorough">
/// Activity of the thorough check. Its <see cref="CheckActivity.Runs"/> counts how often
/// the check was asked to run; the calls under <see cref="LlmOperation.FaithfulnessCheck"/>
/// count how often it actually reached the model. Runs without calls mean the check was
/// toggled off or had nothing to check.
/// </param>
/// <param name="ThoroughNotEvaluated">
/// Thorough runs that reached the model and came back unreadable. Those runs verified
/// nothing, so they are counted apart from runs that genuinely found no claims.
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
    /// <summary>An empty report - nothing ran.</summary>
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

    /// <summary>Calls whose usage the provider did not report in full - the token total is a lower bound by that many calls.</summary>
    public int CallsWithoutUsage => Llm.Values.Sum(usage => usage.CallsWithoutUsage);

    /// <summary>Calls and tokens for one operation; none if it never ran.</summary>
    public LlmUsage UsageOf(LlmOperation operation)
        => Llm.TryGetValue(operation, out LlmUsage? usage) ? usage : LlmUsage.None;
}
