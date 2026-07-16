namespace Chartula.Cli.Configuration;

/// <summary>
/// How the GitHub API is reached. As with the LLM key, the token is never stored
/// here, only the name of the environment variable to read it from. The base URL
/// is configurable for GitHub Enterprise.
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:GitHub";

    /// <summary>The GitHub REST API base URL (override for GitHub Enterprise).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.github.com/";

    /// <summary>Name of the environment variable holding the API token.</summary>
    public string TokenEnvironmentVariable { get; init; } = "GITHUB_TOKEN";
}
