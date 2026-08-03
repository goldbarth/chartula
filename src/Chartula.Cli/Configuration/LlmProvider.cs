namespace Chartula.Cli.Configuration;

/// <summary>
/// Which provider backs the LLM seam. Two entries, not two vendors: the second is
/// a dialect rather than a company, and every endpoint that speaks it - hosted or
/// running on the user's own machine - is reached through the same adapter.
/// </summary>
public enum LlmProvider
{
    /// <summary>Anthropic's first-party API.</summary>
    Anthropic,

    /// <summary>
    /// Any endpoint speaking the OpenAI chat-completions dialect, addressed by
    /// <see cref="LlmOptions.BaseUrl"/>. Ollama, LM Studio, llama.cpp and vLLM all
    /// serve it, as do the hosted providers that advertise OpenAI compatibility.
    /// The dialect is uniform about chat completion and not about JSON schema, so
    /// what an endpoint does with structured output is a property of that endpoint,
    /// not of this value.
    /// </summary>
    OpenAiCompatible,
}

/// <summary>
/// Parses the configured provider. An unknown name is an error rather than a
/// fallback: silently reaching a different provider than the one asked for would
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
    /// The spelling this provider has in configuration. Aliases and casing are
    /// accepted on the way in, so echoing the raw value back in a message would
    /// quote the user at themselves rather than name the key they need to set.
    /// </summary>
    public static string ToConfigurationValue(LlmProvider provider) => provider switch
    {
        LlmProvider.Anthropic => "anthropic",
        LlmProvider.OpenAiCompatible => "openai-compatible",
        _ => throw new InvalidOperationException($"No configuration spelling is defined for provider '{provider}'."),
    };
}
