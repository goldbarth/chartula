using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Commands;

/// <summary>
/// Where a run sends release data and credentials, said before it does. Only the
/// environment can change either, but an environment is easy to inherit without
/// reading it - a shell profile, a CI runner - so the run names what is in force
/// rather than leaving it to be inferred from a failure. Variable names only, never
/// their values.
/// </summary>
internal static class EndpointNotice
{
    public static string For(IConfiguration configuration)
    {
        LlmOptions llm = LlmServiceCollectionExtensions.ReadOptions(configuration);
        GitHubOptions gitHub = GitHubHttpClientFactory.ReadOptions(configuration);

        // Anthropic without a base URL is left on the SDK's own endpoint, which
        // Chartula does not repeat - see LlmProviderDefaults.
        string model = llm.BaseUrl ?? "its default endpoint";
        return $"""
            Model:  {llm.Provider} at {model}, key from {llm.ApiKeyEnvironmentVariable}
            GitHub: {gitHub.ApiBaseUrl}, token from {gitHub.TokenEnvironmentVariable}
            """;
    }
}
