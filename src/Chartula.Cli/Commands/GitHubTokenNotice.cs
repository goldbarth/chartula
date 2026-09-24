using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Commands;

/// <summary>
/// The warning a run starts with when no GitHub token is configured.
/// A token is optional, and a small release fits the unauthenticated rate limit, so
/// the run continues.
/// But the run spends roughly one request per pull request. When the limit runs out,
/// the run fails mid-release with a 403 that names a commit instead of the cause.
/// The warning comes before the work, so continuing without a token is the caller's
/// informed decision.
/// </summary>
internal static class GitHubTokenNotice
{
    /// <summary>Requests GitHub allows per hour, per IP address, without a token.</summary>
    private const int UnauthenticatedRequestsPerHour = 60;

    /// <summary>Requests per hour a token buys.</summary>
    private const int AuthenticatedRequestsPerHour = 5000;

    /// <summary>
    /// Where a fine-grained token is created.
    /// The notice names this instead of <c>gh auth token</c>. That token is the user's
    /// broadest credential, with read and write access to every repository they can
    /// reach, and would be handed to a process that reads repository content.
    /// </summary>
    private const string NewTokenUrl = "https://github.com/settings/personal-access-tokens/new";

    /// <summary>
    /// The notice for this configuration, or <c>null</c> when a token is present.
    /// It names the environment variable as configured, so the message stays correct
    /// when the variable was renamed.
    /// </summary>
    public static string? For(IConfiguration configuration)
    {
        GitHubOptions options = GitHubHttpClientFactory.ReadOptions(configuration);
        string variable = options.TokenEnvironmentVariable;

        if (!string.IsNullOrWhiteSpace(configuration[variable]))
        {
            return null;
        }

        return $"""
            Warning: no GitHub token found in {variable} - the run continues unauthenticated.
              GitHub allows {UnauthenticatedRequestsPerHour} requests an hour per IP address without one, and a run
              spends roughly one per pull request, so a release can exhaust the budget
              partway through. A token raises the limit to {AuthenticatedRequestsPerHour}.
              Create a fine-grained token for this repository at {NewTokenUrl}
              with Contents and Pull requests read-only (Contents read and write to publish
              release notes), then: export {variable}=<token>
            """;
    }
}
