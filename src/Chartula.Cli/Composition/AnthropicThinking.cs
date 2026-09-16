using System.Text.RegularExpressions;
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
internal static partial class AnthropicThinking
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
    /// <exception cref="InvalidOperationException">The model is known to reject the mode.</exception>
    public static Func<IChatClient, object?>? FactoryFor(ThinkingMode mode, string model, int maxOutputTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        EnsureModelAccepts(mode, model);

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

    /// <summary>
    /// Refuses the combinations the API is known to reject, at config load rather than
    /// on the first request - by then the pull requests are fetched and a run that was
    /// never going to work has already started.
    /// </summary>
    /// <remarks>
    /// A model id this cannot read passes through: gateways and new releases name models
    /// in ways no list here anticipates, and refusing those would block runs that work.
    /// The API still rejects a bad combination there, just later.
    /// </remarks>
    private static void EnsureModelAccepts(ThinkingMode mode, string model)
    {
        Match match = ModelIdRegex().Match(model);
        if (!match.Success)
        {
            return;
        }

        string family = match.Groups["family"].Value.ToLowerInvariant();
        int major = int.Parse(match.Groups["major"].Value);
        int minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : 0;

        if (mode == ThinkingMode.Adaptive && (major, minor).CompareTo((4, 6)) < 0)
        {
            throw new InvalidOperationException(
                $"llm.thinking 'adaptive' is not supported by llm.model '{model}'. Adaptive thinking " +
                "needs Claude 4.6 or newer; set llm.thinking to disabled or provider-default, or pick a newer model.");
        }

        if (mode == ThinkingMode.Disabled && family == "fable")
        {
            throw new InvalidOperationException(
                $"llm.thinking 'disabled' is not supported by llm.model '{model}'. Claude Fable always " +
                "thinks and rejects an explicit off; set llm.thinking to provider-default.");
        }
    }

    // Both id shapes Anthropic has used: claude-haiku-4-5 (family first) and
    // claude-3-5-haiku (version first). The minor version is one or two digits so a
    // date suffix (claude-opus-4-20250514) is not read as one. Unanchored, because
    // gateways prefix the id (us.anthropic.claude-...).
    [GeneratedRegex(
        @"claude-(?:(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d{1,2})(?!\d))?|(?<major>\d+)(?:-(?<minor>\d{1,2})(?!\d))?-(?<family>[a-z]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ModelIdRegex();
}
