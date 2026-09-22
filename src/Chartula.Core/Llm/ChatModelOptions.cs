using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The request-shaping knobs <see cref="ChatModel"/> applies to every call. These
/// are provider-agnostic on purpose: which provider backs the client is decided in
/// the composition root, but every provider needs an output ceiling.
/// </summary>
public sealed class ChatModelOptions
{
    /// <summary>
    /// The ceiling on tokens the model may produce per call. Providers require this
    /// and substitute a small default when it is absent, which silently truncates a
    /// changelog mid-sentence, so it is always sent.
    /// <para>
    /// Thinking is produced against this same ceiling, and it goes first. At 16,000
    /// the customer call - the only audience whose shape is specified, so the only
    /// one with a long prompt - spent the whole allowance thinking and was cut off
    /// before it wrote a character: <c>stop_reason max_tokens</c>, a thinking block
    /// and no text block. The value is twice that, so the text has as much room as
    /// the thinking rather than sitting just under an edge.
    /// </para>
    /// </summary>
    public int MaxOutputTokens { get; init; } = 32_000;

    /// <summary>
    /// How much the model reasons, in the provider-neutral form every adapter
    /// translates to its own request field. Null sends nothing, which leaves each
    /// model on its own default.
    /// </summary>
    public ReasoningOptions? Reasoning { get; init; }

    /// <summary>
    /// The model the thorough check asks, sent per call; <c>null</c> leaves the
    /// client's own model, the one that renders.
    /// </summary>
    public string? CheckModelId { get; init; }

    /// <summary>
    /// How much the thorough check's model reasons. Separate from
    /// <see cref="Reasoning"/> because the check answers a short verdict, and thinking
    /// there has been measured to cost thousands of tokens without finding more.
    /// </summary>
    public ReasoningOptions? CheckReasoning { get; init; }
}
