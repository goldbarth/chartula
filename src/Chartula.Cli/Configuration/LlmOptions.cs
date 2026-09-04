namespace Chartula.Cli.Configuration;

/// <summary>
/// How the LLM is wired, driven from <c>chartula.yaml</c> / environment. Only the
/// provider selection and model live here; the API key is never stored, only the
/// name of the environment variable to read it from.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Llm";

    /// <summary>
    /// The provider to use: <c>anthropic</c> or <c>openai-compatible</c>. See
    /// <see cref="LlmProviderParser"/>.
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>
    /// The model id passed to the provider. The default shown here is Anthropic's;
    /// what applies for a given provider comes from <see cref="LlmProviderDefaults"/>,
    /// and not every provider has one.
    /// </summary>
    public string Model { get; init; } = "claude-opus-4-8";

    /// <summary>Name of the environment variable holding the API key.</summary>
    public string ApiKeyEnvironmentVariable { get; init; } = "ANTHROPIC_API_KEY";

    /// <summary>
    /// The endpoint the provider is reached at, in the shape
    /// <see cref="GitHubOptions.ApiBaseUrl"/> uses for GitHub Enterprise. Null leaves
    /// the provider on its own default, which is what Anthropic does; for
    /// <c>openai-compatible</c> there is no default and this has to be set.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// The ceiling on tokens the model may produce per call. Raise it for releases
    /// whose changelog runs long; a too-low ceiling truncates the text mid-sentence,
    /// or leaves none at all when the model spends the allowance thinking - see
    /// <see cref="Core.Llm.ChatModelOptions.MaxOutputTokens"/>.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 32_000;

    /// <summary>
    /// Whether the model thinks before answering: <c>provider-default</c>,
    /// <c>disabled</c>, or <c>adaptive</c>. Unset leaves each model on its own
    /// default, which is not the same across models - see
    /// <see cref="ThinkingModeParser"/>.
    /// </summary>
    public string? Thinking { get; init; }
}
