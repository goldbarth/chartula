using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Commands;

/// <summary>
/// The warning a run starts with when no GitHub token is configured. A token is
/// optional and a small release fits the unauthenticated budget, so the run is not
/// refused - but the budget is spent per pull request, and when it runs out the
/// failure lands mid-release as a 403 that names a commit rather than the cause.
/// Saying so before the work starts turns that into something the caller decided.
/// </summary>
internal static class GitHubTokenNotice
{
    /// <summary>Requests GitHub allows per hour, per IP address, without a token.</summary>
    private const int UnauthenticatedRequestsPerHour = 60;

    /// <summary>Requests per hour a token buys.</summary>
    private const int AuthenticatedRequestsPerHour = 5000;

    /// <summary>
    /// The notice for a run configured this way, or <c>null</c> when a token is
    /// present and there is nothing to warn about. The environment variable is
    /// named as configured, so the message stays true when it was renamed.
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
              Set one with: export {variable}=$(gh auth token)
            """;
    }
}
