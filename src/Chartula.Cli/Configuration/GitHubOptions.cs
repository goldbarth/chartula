namespace Chartula.Cli.Configuration;

/// <summary>
/// How the GitHub API is reached.
/// As with the LLM key, the token is never stored here, only the name of the
/// environment variable it is read from.
/// The base URL is configurable for GitHub Enterprise.
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:GitHub";

    /// <summary>The environment variable that sets <see cref="ApiBaseUrl"/>, the only place it can be set.</summary>
    public const string ApiBaseUrlVariable = "Chartula__GitHub__ApiBaseUrl";

    /// <summary>The GitHub REST API base URL (override for GitHub Enterprise).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";

    /// <summary>Name of the environment variable holding the API token.</summary>
    public string TokenEnvironmentVariable { get; init; } = "GITHUB_TOKEN";
}
