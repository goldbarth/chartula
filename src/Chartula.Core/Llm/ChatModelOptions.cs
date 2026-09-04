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
    /// An optional hook for provider-specific request fields that have no
    /// provider-agnostic equivalent - thinking being the one Chartula needs. The
    /// object it returns is the provider's own request type, so only the composition
    /// root can build one; this type carries the delegate without naming a provider.
    /// Null sends nothing extra, which leaves each model on its own default.
    /// </summary>
    public Func<IChatClient, object?>? RawRepresentationFactory { get; init; }
}
