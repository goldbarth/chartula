namespace Chartula.Cli.Configuration;

/// <summary>
/// How the LLM is wired, configured from <c>chartula.yaml</c> or the environment.
/// The API key is never stored here, only the name of the environment variable it
/// is read from.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Llm";

    /// <summary>The environment variable that sets <see cref="BaseUrl"/>, the only place it can be set.</summary>
    public const string BaseUrlVariable = "Chartula__Llm__BaseUrl";

    /// <summary>
    /// The provider to use: <c>anthropic</c> or <c>openai-compatible</c>. See
    /// <see cref="LlmProviderParser"/>.
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>
    /// The model id passed to the provider. The default here is Anthropic's.
    /// The actual default per provider comes from <see cref="LlmProviderDefaults"/>,
    /// and not every provider has one.
    /// </summary>
    public string Model { get; init; } = LlmProviderDefaults.AnthropicModel;

    /// <summary>Name of the environment variable holding the API key.</summary>
    public string ApiKeyEnvironmentVariable { get; init; } = "ANTHROPIC_API_KEY";

    /// <summary>
    /// The endpoint of the provider, in the same form as <see cref="GitHubOptions.ApiBaseUrl"/>
    /// for GitHub Enterprise.
    /// <c>null</c> uses the provider's own default endpoint, as for Anthropic.
    /// <c>openai-compatible</c> has no default, so this must be set.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// The maximum number of tokens the model may produce per call. Raise it for releases
    /// with a long changelog.
    /// A value too low cuts the text off mid-sentence, or leaves no text at all when the
    /// model spends the whole limit on thinking
    /// (see <see cref="Core.Llm.ChatModelOptions.MaxOutputTokens"/>).
    /// </summary>
    public int MaxOutputTokens { get; init; } = 32_000;

    /// <summary>
    /// How much the model reasons before answering: <c>provider-default</c>,
    /// <c>disabled</c>, <c>low</c>, <c>medium</c>, <c>high</c> or <c>xhigh</c>, the same
    /// values for every provider.
    /// Unset uses each model's own default, which differs between models
    /// (see <see cref="ThinkingModeParser"/>).
    /// </summary>
    public string? Thinking { get; init; }
}
