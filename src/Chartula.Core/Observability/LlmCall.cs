namespace Chartula.Core.Observability;

/// <summary>
/// One model call as the run records it. Token counts are what the provider reported.
/// A <c>null</c> count means the provider did not report it.
/// </summary>
/// <param name="InputTokens">Tokens sent, as reported.</param>
/// <param name="OutputTokens">Tokens produced, as reported.</param>
public sealed record LlmCall(long? InputTokens, long? OutputTokens)
{
    /// <summary>
    /// The part of <see cref="InputTokens"/> the provider served from its prompt cache,
    /// usually at a lower price. <c>null</c> when the provider did not report it.
    /// </summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>
    /// The part of <see cref="OutputTokens"/> the model spent reasoning instead of answering.
    /// <c>null</c> when the provider does not report it separately.
    /// </summary>
    public long? ReasoningTokens { get; init; }

    /// <summary>How long the call took, retries included.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The requests the transport sent for the call, or <c>null</c> when they could
    /// not be observed. A value of 1 means the call was not retried.
    /// </summary>
    public int? Attempts { get; init; }

    /// <summary>
    /// The call ended in an error instead of an answer. It still took time, but no usage
    /// came back, so it is counted separately from the calls that answered.
    /// </summary>
    public bool Failed { get; init; }
}
