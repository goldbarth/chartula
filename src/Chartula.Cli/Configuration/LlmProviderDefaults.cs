namespace Chartula.Cli.Configuration;

/// <summary>
/// What each provider assumes when the user says nothing. A default is only
/// written here when it is right for that provider; a <c>null</c> means there is
/// no honest default and the value has to be configured.
/// </summary>
/// <param name="Model">The model id, or <c>null</c> when no id can be assumed.</param>
/// <param name="ApiKeyEnvironmentVariable">Name of the environment variable holding the key.</param>
/// <param name="BaseUrl">The endpoint, or <c>null</c> to leave the provider on its own.</param>
internal sealed record LlmProviderDefaults(
    string? Model,
    string ApiKeyEnvironmentVariable,
    string? BaseUrl)
{
    /// <summary>The defaults for the given provider.</summary>
    public static LlmProviderDefaults For(LlmProvider provider) => provider switch
    {
        LlmProvider.Anthropic => new LlmProviderDefaults(
            Model: "claude-opus-4-8",
            ApiKeyEnvironmentVariable: "ANTHROPIC_API_KEY",
            // Null, not the literal URL: the Anthropic client already knows where its
            // own API lives, and repeating it here would pin a value the SDK is free
            // to change. Setting llm.baseUrl overrides it, for a proxy or a gateway.
            BaseUrl: null),

        // Neither the model nor the endpoint can be guessed. An endpoint default
        // would be the worse of the two: this provider exists so release data can
        // stay on the user's machine, and a default pointing at a hosted API would
        // send it off the machine for anyone who set only the model. Both are
        // therefore required, and the error names them.
        LlmProvider.OpenAiCompatible => new LlmProviderDefaults(
            Model: null,
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            BaseUrl: null),

        _ => throw new InvalidOperationException($"No defaults are defined for provider '{provider}'."),
    };
}
