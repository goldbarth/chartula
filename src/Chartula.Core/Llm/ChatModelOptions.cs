using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The request settings <see cref="ChatModel"/> applies to every call.
/// They are provider-agnostic on purpose: the composition root decides which provider
/// backs the client, but every provider needs an output ceiling.
/// </summary>
public sealed class ChatModelOptions
{
    /// <summary>
    /// The maximum number of tokens the model may produce per call. It is always sent:
    /// providers require it, and without it they use a small default that silently
    /// cuts a changelog off mid-sentence.
    /// <para>
    /// Thinking counts against the same limit, and it comes first. At 16,000, the
    /// customer call spent the whole limit on thinking and was cut off before it wrote
    /// a character: <c>stop_reason max_tokens</c>, a thinking block and no text block.
    /// The customer call is the only audience with a specified shape, so it is the only
    /// one with a long prompt.
    /// The value is twice 16,000, so the text has as much room as the thinking, with a
    /// safe margin.
    /// </para>
    /// </summary>
    public int MaxOutputTokens { get; init; } = 32_000;

    /// <summary>
    /// How much the model reasons, in the provider-neutral form every adapter
    /// translates to its own request field.
    /// <c>null</c> sends nothing, so each model uses its own default.
    /// </summary>
    public ReasoningOptions? Reasoning { get; init; }

    /// <summary>
    /// The model for the thorough check, sent per call.
    /// <c>null</c> uses the client's own model, the one that renders.
    /// </summary>
    public string? CheckModelId { get; init; }

    /// <summary>
    /// How much the thorough check's model reasons.
    /// It is separate from <see cref="Reasoning"/> because the check answers with a
    /// short verdict. Measurements showed that thinking there costs thousands of tokens
    /// without finding more.
    /// </summary>
    public ReasoningOptions? CheckReasoning { get; init; }
}
