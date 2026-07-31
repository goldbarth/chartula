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
    public static Func<IChatClient, object?>? FactoryFor(ThinkingMode mode)
    {
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

        // Model, messages and the output ceiling come from the request the adapter is
        // already building; these placeholders exist only to satisfy the required
        // members of the provider's request type and are overwritten.
        return _ => new MessageCreateParams
        {
            Model = "placeholder",
            MaxTokens = 1,
            Messages = [],
            Thinking = thinking,
        };
    }
}
