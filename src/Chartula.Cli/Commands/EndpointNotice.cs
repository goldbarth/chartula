using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Commands;

/// <summary>
/// Says where a run sends release data and credentials, before it does.
/// Only the environment can change either, but an environment is easy to inherit
/// unread, for example from a shell profile or a CI runner. So the run names what is
/// in effect, instead of leaving it to be inferred from a failure.
/// It names variables only, never their values.
/// It also names the model and the thinking mode: they decide what a run costs, and a
/// run that states them can be compared with another run.
/// </summary>
internal static class EndpointNotice
{
    public static string For(IConfiguration configuration)
    {
        LlmOptions llm = LlmServiceCollectionExtensions.ReadOptions(configuration);
        GitHubOptions gitHub = GitHubHttpClientFactory.ReadOptions(configuration);

        // Anthropic without a base URL uses the SDK's own endpoint, and Chartula does
        // not duplicate that URL (see LlmProviderDefaults).
        string endpoint = llm.BaseUrl ?? "its default endpoint";
        string thinking = ThinkingModeParser.Name(ThinkingModeParser.Parse(llm.Thinking));

        // Name the check's model only when it differs. Otherwise the line above already
        // says what the check runs with.
        FaithfulnessOptions faithfulness = ThoroughCheckModel.Read(configuration);
        ThoroughCheckModel check = ThoroughCheckModel.Resolve(llm, faithfulness);
        string checkLine = faithfulness.Thorough && check.DiffersFrom(llm)
            ? $"\n        thorough check: {check.Model}, thinking {ThinkingModeParser.Name(check.Thinking)}"
            : string.Empty;

        return $"""
            Model:  {llm.Provider} at {endpoint}, {Credential(configuration, "key", llm.ApiKeyEnvironmentVariable)}
                    {llm.Model}, thinking {thinking}{checkLine}
            GitHub: {gitHub.ApiBaseUrl}, {Credential(configuration, "token", gitHub.TokenEnvironmentVariable)}
            """;
    }

    // An unset variable is not a source: "key from ANTHROPIC_API_KEY" above a warning that
    // no key was found reads as a contradiction (#210). The variable is still named, so
    // the line says what to set.
    private static string Credential(IConfiguration configuration, string credential, string variable)
        => string.IsNullOrWhiteSpace(configuration[variable])
            ? $"unauthenticated, {variable} is not set"
            : $"{credential} from {variable}";
}
