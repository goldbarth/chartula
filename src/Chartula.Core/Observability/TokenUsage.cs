namespace Chartula.Core.Observability;

/// <summary>Tokens consumed by one or more LLM calls.</summary>
/// <param name="InputTokens">Tokens sent to the model.</param>
/// <param name="OutputTokens">Tokens produced by the model.</param>
public readonly record struct TokenUsage(long InputTokens, long OutputTokens)
{
    /// <summary>No tokens.</summary>
    public static TokenUsage None => default;

    /// <summary>Input and output tokens combined.</summary>
    public long TotalTokens => InputTokens + OutputTokens;

    public static TokenUsage operator +(TokenUsage left, TokenUsage right)
        => new(left.InputTokens + right.InputTokens, left.OutputTokens + right.OutputTokens);
}
