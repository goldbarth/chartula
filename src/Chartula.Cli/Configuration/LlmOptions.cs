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

    /// <summary>The provider to use. Only <c>anthropic</c> is implemented today.</summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>The model id passed to the provider.</summary>
    public string Model { get; init; } = "claude-opus-4-8";

    /// <summary>Name of the environment variable holding the API key.</summary>
    public string ApiKeyEnvironmentVariable { get; init; } = "ANTHROPIC_API_KEY";

    /// <summary>
    /// The ceiling on tokens the model may produce per call. Raise it for releases
    /// whose changelog runs long; a too-low ceiling truncates the text mid-sentence.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 16_000;
}
