namespace Chartula.Core.Observability;

/// <summary>
/// One model call as the run records it. Tokens are what the provider reported;
/// a null count means it did not report that side.
/// </summary>
/// <param name="InputTokens">Tokens sent, as reported.</param>
/// <param name="OutputTokens">Tokens produced, as reported.</param>
public sealed record LlmCall(long? InputTokens, long? OutputTokens)
{
    /// <summary>
    /// The part of <see cref="InputTokens"/> the provider served from its prompt
    /// cache, usually at a lower price; <c>null</c> when it did not say.
    /// </summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>
    /// The part of <see cref="OutputTokens"/> the model spent reasoning rather than
    /// answering; <c>null</c> when the provider does not report it separately.
    /// </summary>
    public long? ReasoningTokens { get; init; }

    /// <summary>How long the call took, retries included.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The requests the transport sent for the call, or <c>null</c> when it could
    /// not be observed. One is a call that was not retried.
    /// </summary>
    public int? Attempts { get; init; }

    /// <summary>
    /// The call ended in an error rather than an answer. Its time is spent, but no
    /// usage came back, so it is counted apart from the calls that answered.
    /// </summary>
    public bool Failed { get; init; }
}
