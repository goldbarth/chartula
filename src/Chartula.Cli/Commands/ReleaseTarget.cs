using System.Text.RegularExpressions;
using Chartula.Core.PullRequests;

namespace Chartula.Cli.Commands;

/// <summary>
/// The tag and repository a run is for.
/// A run must start from a checkout of the repository, because git reads the commit
/// range there. So both values default from that checkout, and <c>--tag</c> and
/// <c>--repo</c> only override them.
/// A default is announced before the run, because <c>generate</c> publishes to it, and
/// nobody should find out afterwards which release they wrote to.
/// </summary>
/// <param name="Tag">The release tag.</param>
/// <param name="Repository">The GitHub repository the pull requests are read from.</param>
internal sealed partial record ReleaseTarget(string Tag, RepositoryCoordinates Repository)
{
    /// <summary>The remote a repository defaults from.</summary>
    public const string Remote = "origin";

    // user@host:owner/name: git's scp-like form, which is not a URI.
    [GeneratedRegex(@"^(?:[^@/\s]+@)?[^:/\s]+:(?<path>[^/\\].*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpLikeRemote();

    /// <summary>
    /// Resolves the target from the arguments, and reads what they leave out from the checkout.
    /// On failure, writes the reason to <paramref name="error"/> and returns <c>null</c>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="directory">The checkout, named in messages.</param>
    /// <param name="readNearestTag">Reads the nearest tag reachable from <c>HEAD</c>.</param>
    /// <param name="readRemoteUrl">Reads the URL of <see cref="Remote"/>.</param>
    /// <param name="error">Where announcements and errors go: stderr, so they stay out of the changelog.</param>
    public static async Task<ReleaseTarget?> ResolveAsync(
        IReadOnlyList<string> args,
        string directory,
        Func<Task<string?>> readNearestTag,
        Func<Task<string?>> readRemoteUrl,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(readNearestTag);
        ArgumentNullException.ThrowIfNull(readRemoteUrl);
        ArgumentNullException.ThrowIfNull(error);

        string? repoOption = CommandLineArguments.GetOption(args, "--repo");
        RepositoryCoordinates repository;
        bool repositoryDefaulted = string.IsNullOrWhiteSpace(repoOption);
        if (!repositoryDefaulted)
        {
            if (!ReleaseCommand.TryParseRepository(repoOption, out repository))
            {
                error.WriteLine($"Invalid option --repo '{repoOption}'. Expected <owner/name>.");
                return null;
            }
        }
        else
        {
            string? url = await readRemoteUrl();
            if (url is null)
            {
                error.WriteLine(
                    $"No --repo given, and '{directory}' has no '{Remote}' remote to read it from. " +
                    "Pass --repo <owner/name>, or run from a checkout of the repository.");
                return null;
            }

            if (!TryParseRemoteUrl(url, out repository))
            {
                error.WriteLine(
                    $"No --repo given, and the '{Remote}' remote '{url}' does not name an <owner/name> repository. " +
                    "Pass --repo <owner/name>.");
                return null;
            }
        }

        string? tag = CommandLineArguments.GetOption(args, "--tag");
        bool tagDefaulted = string.IsNullOrWhiteSpace(tag);
        if (tagDefaulted)
        {
            tag = await readNearestTag();
            if (tag is null)
            {
                error.WriteLine(
                    $"No --tag given, and no tag is reachable from HEAD in '{directory}'. " +
                    "Pass --tag <release-tag>, or run from a checkout of the repository with its tags fetched (git fetch --tags).");
                return null;
            }
        }

        if (tagDefaulted)
        {
            error.WriteLine($"Using tag {tag}, the nearest tag reachable from HEAD. Pass --tag to choose another.");
        }

        if (repositoryDefaulted)
        {
            error.WriteLine(
                $"Using repository {repository.Owner}/{repository.Name}, from the '{Remote}' remote. " +
                "Pass --repo to choose another.");
        }

        return new ReleaseTarget(tag!, repository);
    }

    /// <summary>
    /// Owner and name from a remote URL, in the forms git accepts for a hosted
    /// repository: <c>https://host/owner/name</c>, <c>ssh://git@host/owner/name</c>
    /// and <c>git@host:owner/name</c>, each with or without <c>.git</c>.
    /// The host is not checked, so a GitHub Enterprise remote resolves the same way.
    /// </summary>
    public static bool TryParseRemoteUrl(string? url, out RepositoryCoordinates repository)
    {
        repository = new RepositoryCoordinates(string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string value = url.Trim();
        string? path = null;
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            if (uri.Scheme is "https" or "http" or "ssh" or "git")
            {
                path = Uri.UnescapeDataString(uri.AbsolutePath);
            }
        }
        else if (ScpLikeRemote().Match(value) is { Success: true } match)
        {
            path = match.Groups["path"].Value;
        }

        if (path is null)
        {
            return false;
        }

        path = path.Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^".git".Length];
        }

        string[] segments = path.Split('/');
        if (segments.Length != 2 || segments.Any(segment => segment.Length == 0))
        {
            return false;
        }

        repository = new RepositoryCoordinates(segments[0], segments[1]);
        return true;
    }
}
