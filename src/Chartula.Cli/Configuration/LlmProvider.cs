namespace Chartula.Cli.Configuration;

/// <summary>
/// Which provider backs the LLM seam.
/// The two entries are not two vendors: the second is a dialect, not a company.
/// Every endpoint that speaks it, hosted or on the user's own machine, is reached
/// through the same adapter.
/// </summary>
public enum LlmProvider
{
    /// <summary>Anthropic's first-party API.</summary>
    Anthropic,

    /// <summary>
    /// Any endpoint speaking the OpenAI chat-completions dialect, addressed by
    /// <see cref="LlmOptions.BaseUrl"/>. Ollama, LM Studio, llama.cpp and vLLM all serve
    /// it, as do the hosted providers that advertise OpenAI compatibility.
    /// The dialect is uniform for chat completion but not for JSON schema. So how an
    /// endpoint handles structured output depends on the endpoint, not on this value.
    /// </summary>
    OpenAiCompatible,
}

/// <summary>
/// Parses the configured provider.
/// An unknown name is an error, not a fallback: silently using another provider would
/// send the user's release data somewhere they did not choose.
/// </summary>
public static class LlmProviderParser
{
    /// <summary>The default when the provider is not configured.</summary>
    public const LlmProvider Default = LlmProvider.Anthropic;

    /// <summary>
    /// Maps a configuration value to an <see cref="LlmProvider"/>. <c>null</c> or
    /// blank yields <see cref="Default"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not recognized.</exception>
    public static LlmProvider Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "anthropic" => LlmProvider.Anthropic,
            "openaicompatible" => LlmProvider.OpenAiCompatible,
            _ => throw new InvalidOperationException(
                $"Unknown llm.provider '{value}'. Valid values: anthropic, openai-compatible."),
        };
    }

    /// <summary>
    /// The provider's spelling in configuration.
    /// Parsing accepts aliases and any casing, so messages use this spelling instead of
    /// echoing the raw value. That names the value the user needs to set.
    /// </summary>
    public static string ToConfigurationValue(LlmProvider provider) => provider switch
    {
        LlmProvider.Anthropic => "anthropic",
        LlmProvider.OpenAiCompatible => "openai-compatible",
        _ => throw new InvalidOperationException($"No configuration spelling is defined for provider '{provider}'."),
    };
}
