namespace Chartula.Cli.Configuration;

/// <summary>
/// The values each provider uses when the user configures nothing.
/// A default is written here only when it is right for that provider. <c>null</c>
/// means no safe default exists and the value must be configured.
/// </summary>
/// <param name="Model">The model id, or <c>null</c> when no id can be assumed.</param>
/// <param name="ApiKeyEnvironmentVariable">Name of the environment variable holding the key.</param>
/// <param name="BaseUrl">The endpoint, or <c>null</c> to use the provider's own default endpoint.</param>
internal sealed record LlmProviderDefaults(
    string? Model,
    string ApiKeyEnvironmentVariable,
    string? BaseUrl)
{
    /// <summary>
    /// The model a run uses when none is configured. This is the only place it is named.
    /// Sonnet 5 instead of an Opus tier: this repository's own runs cost $0.25-0.29 on
    /// it, and every adopter's first run uses the default.
    /// A full id instead of an alias, so the model cannot change under a run without a
    /// change here.
    /// </summary>
    public const string AnthropicModel = "claude-sonnet-5";

    /// <summary>The defaults for the given provider.</summary>
    public static LlmProviderDefaults For(LlmProvider provider) => provider switch
    {
        LlmProvider.Anthropic => new LlmProviderDefaults(
            Model: AnthropicModel,
            ApiKeyEnvironmentVariable: "ANTHROPIC_API_KEY",
            // Null instead of the literal URL: the Anthropic client already knows its own
            // endpoint, and repeating it here would pin a value the SDK may change.
            // Chartula__Llm__BaseUrl overrides it for a proxy or a gateway.
            BaseUrl: null),

        // Neither the model nor the endpoint can be guessed, so both are required and
        // the error names them.
        // An endpoint default would be worse: this provider exists so release data can
        // stay on the user's machine. A default pointing at a hosted API would send the
        // data off the machine for anyone who set only the model.
        LlmProvider.OpenAiCompatible => new LlmProviderDefaults(
            Model: null,
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            BaseUrl: null),

        _ => throw new InvalidOperationException($"No defaults are defined for provider '{provider}'."),
    };
}
