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
        ArgumentNullException.ThrowIfNull(error);

        Resolved<RepositoryCoordinates> repository = await ResolveRepositoryAsync(args, directory, readRemoteUrl);
        if (repository.Value is null)
        {
            error.WriteLine(repository.Message);
            return null;
        }

        Resolved<string> tag = await ResolveTagAsync(args, directory, readNearestTag);
        if (tag.Value is null)
        {
            error.WriteLine(tag.Message);
            return null;
        }

        foreach (string? announcement in (string?[])[tag.Message, repository.Message])
        {
            if (announcement is not null)
            {
                error.WriteLine(announcement);
            }
        }

        return new ReleaseTarget(tag.Value, repository.Value);
    }

    /// <summary>
    /// What is wrong with the value of <c>--repo</c>, or null. It is read from the
    /// arguments alone, so a run can refuse it where it refuses every other argument:
    /// before its header, and before anything is read (#353).
    /// </summary>
    public static string? CheckRepositoryOption(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? repoOption = CommandLineArguments.GetOption(args, "--repo");
        return string.IsNullOrWhiteSpace(repoOption) || ReleaseCommand.TryParseRepository(repoOption, out _)
            ? null
            : $"Invalid option --repo '{repoOption}'. Expected <owner/name>.";
    }

    /// <summary>
    /// The repository from <c>--repo</c>, or from the <see cref="Remote"/> remote when it
    /// is not passed.
    /// Without a value, <see cref="Resolved{T}.Message"/> says why and what to pass.
    /// With a default, it announces where the value came from.
    /// </summary>
    public static async Task<Resolved<RepositoryCoordinates>> ResolveRepositoryAsync(
        IReadOnlyList<string> args,
        string directory,
        Func<Task<string?>> readRemoteUrl)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(readRemoteUrl);

        string? repoOption = CommandLineArguments.GetOption(args, "--repo");
        if (!string.IsNullOrWhiteSpace(repoOption))
        {
            return ReleaseCommand.TryParseRepository(repoOption, out RepositoryCoordinates passed)
                ? new(passed, null)
                : new(null, CheckRepositoryOption(args));
        }

        string? url = await readRemoteUrl();
        if (url is null)
        {
            return new(null,
                $"No --repo given, and '{directory}' has no '{Remote}' remote to read it from. " +
                "Pass --repo <owner/name>, or run from a checkout of the repository.");
        }

        if (!TryParseRemoteUrl(url, out RepositoryCoordinates repository))
        {
            return new(null,
                $"No --repo given, and the '{Remote}' remote '{url}' does not name an <owner/name> repository. " +
                "Pass --repo <owner/name>.");
        }

        return new(repository,
            $"Using repository {repository.Owner}/{repository.Name}, from the '{Remote}' remote. " +
            "Pass --repo to choose another.");
    }

    /// <summary>
    /// The tag from <c>--tag</c>, or the nearest tag reachable from <c>HEAD</c> when it is
    /// not passed, with a message as in <see cref="ResolveRepositoryAsync"/>.
    /// </summary>
    public static async Task<Resolved<string>> ResolveTagAsync(
        IReadOnlyList<string> args,
        string directory,
        Func<Task<string?>> readNearestTag)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(readNearestTag);

        string? tag = CommandLineArguments.GetOption(args, "--tag");
        if (!string.IsNullOrWhiteSpace(tag))
        {
            return new(tag, null);
        }

        tag = await readNearestTag();
        return tag is null
            ? new(null,
                $"No --tag given, and no tag is reachable from HEAD in '{directory}'. " +
                "Pass --tag <release-tag>, or run from a checkout of the repository with its tags fetched (git fetch --tags).")
            : new(tag, $"Using tag {tag}, the nearest tag reachable from HEAD. Pass --tag to choose another.");
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

/// <summary>
/// A value read from the arguments or the checkout, with what to tell the operator:
/// why it is missing, or where a default came from. <c>null</c> when there is nothing to say.
/// </summary>
internal sealed record Resolved<T>(T? Value, string? Message)
    where T : class;
