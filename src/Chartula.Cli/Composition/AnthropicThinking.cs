using Anthropic.Models.Messages;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.AI;

namespace Chartula.Cli.Composition;

/// <summary>
/// Turns a <see cref="ThinkingMode"/> into the provider-specific request fragment
/// that carries it. Thinking has no provider-agnostic equivalent in
/// <see cref="Microsoft.Extensions.AI"/>, so it travels through the raw-representation
/// hook - and that hook is the only place the Anthropic request type is named, which
/// keeps the domain free of the provider package.
/// </summary>
internal static class AnthropicThinking
{
    /// <summary>
    /// The factory for the given mode, or null for
    /// <see cref="ThinkingMode.ProviderDefault"/> - sending no thinking field at all
    /// is what leaves each model on its own default.
    /// </summary>
    /// <remarks>
    /// The model and the output ceiling have to be passed in, because a raw fragment
    /// is not merged with the rest of the request: the adapter appends the messages to
    /// it and otherwise takes it as given, so <c>ChatOptions.ModelId</c> and
    /// <c>ChatOptions.MaxOutputTokens</c> stop being applied the moment this factory
    /// exists. Whatever the fragment says is what the provider is asked for. Verified
    /// against a live call - an earlier version left a placeholder model here and the
    /// API dutifully answered <c>model: placeholder</c>.
    /// </remarks>
    public static Func<IChatClient, object?>? FactoryFor(ThinkingMode mode, string model, int maxOutputTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        ThinkingConfigParam? thinking = mode switch
        {
            ThinkingMode.Disabled => new ThinkingConfigDisabled(),
            ThinkingMode.Adaptive => new ThinkingConfigAdaptive(),
            _ => null,
        };

        if (thinking is null)
        {
            return null;
        }

        // Messages are the one field the adapter merges, so an empty list here is
        // correct: it appends the real conversation to it.
        return _ => new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxOutputTokens,
            Messages = [],
            Thinking = thinking,
        };
    }
}
