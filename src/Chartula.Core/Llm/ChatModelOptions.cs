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
    /// changelog mid-sentence, so it is always sent. The default leaves room for the
    /// longest audience text while staying under the HTTP timeout a non-streaming
    /// request is subject to.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 16_000;
}
