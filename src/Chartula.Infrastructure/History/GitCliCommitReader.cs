using System.Globalization;
using Chartula.Core.History;

namespace Chartula.Infrastructure.History;

/// <summary>
/// An <see cref="IReleaseCommitReader"/> that reads history by invoking the
/// <c>git</c> CLI in a repository directory. Shelling out keeps the tool free of
/// native git dependencies, which matters for the native-AOT binaries on the
/// roadmap.
/// </summary>
public sealed class GitCliCommitReader(GitExecutable git, string repositoryPath) : IReleaseCommitReader
{
    // ASCII unit separator: a field delimiter that cannot appear in a hash or a
    // commit subject. Emitted by git's %x1f format token, split on here.
    private const char FieldSeparator = '\u001f';

    // How to get the history a shallow clone left out, for each place it is made.
    private const string FetchFullHistory = """
          Fetch the full history and tags: git fetch --unshallow --tags
          In GitHub Actions, check out with fetch-depth: 0; in GitLab CI, set GIT_DEPTH: 0.
        """;

    public async Task<CommitRange> ReadReleaseCommitsAsync(
        string tag,
        string? since = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("A release tag is required.", nameof(tag));
        }

        GitResult verify = await RunGitAsync(
            ["rev-parse", "--verify", "--quiet", $"{tag}^{{commit}}"], cancellationToken);
        if (verify.ExitCode != 0)
        {
            throw new InvalidOperationException(await DescribeMissingTagAsync(tag, cancellationToken));
        }

        // A shallow clone - the default of actions/checkout and GitLab CI - ends its
        // history at the fetch depth, where a first tag's would end. Neither the
        // previous tag nor the whole history can be read from it, only a start that
        // was fetched along with the tag.
        bool shallow = await IsShallowAsync(cancellationToken);
        if (shallow && string.IsNullOrWhiteSpace(since))
        {
            throw new InvalidOperationException($"""
                The checkout is a shallow clone: its history ends at the fetch depth, not where '{tag}' starts, so neither the previous tag nor the whole history can be read from it.
                {FetchFullHistory}
                """);
        }

        string? from = string.IsNullOrWhiteSpace(since)
            ? await ReadPreviousTagAsync(tag, cancellationToken)
            : await VerifyStartAsync(since.Trim(), tag, shallow, cancellationToken);

        string range = from is null ? tag : $"{from}..{tag}";
        GitResult log = await RunGitAsync(
            ["log", "--no-color", "--pretty=format:%H%x1f%s", range], cancellationToken);
        if (log.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to read commits for '{range}': {log.StandardError.Trim()}");
        }

        DateOnly? taggedAt = await ReadTagDateAsync(tag, cancellationToken);
        return new CommitRange(tag, from, ParseCommits(log.StandardOutput), taggedAt);
    }

    /// <summary>
    /// Why the tag was not found, in terms the caller can act on. The history is read
    /// from the directory the run starts in, so the likely cause is starting it in the
    /// wrong place - outside any repository, in another checkout, or in a clone
    /// without the tag - and the message names the place and what it holds.
    /// </summary>
    private async Task<string> DescribeMissingTagAsync(string tag, CancellationToken cancellationToken)
    {
        GitResult root = await RunGitAsync(["rev-parse", "--show-toplevel"], cancellationToken);
        if (root.ExitCode != 0)
        {
            return $"""
                '{Path.GetFullPath(repositoryPath)}' is not inside a git repository.
                  Run chartula from a checkout of the repository the release belongs to.
                """;
        }

        // The newest tags show a typo or a naming scheme at a glance; none at all is
        // a clone that never fetched them.
        GitResult tags = await RunGitAsync(["tag", "--sort=-creatordate"], cancellationToken);
        string[] latest = tags.ExitCode == 0
            ? tags.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        string found = latest.Length == 0
            ? "It has no tags."
            : $"Its latest tags: {string.Join(", ", latest.Take(3))}.";

        return $"""
            Tag '{tag}' is not in the checkout at {root.StandardOutput.Trim()}.
              {found}
              Run from a checkout of the repository the release belongs to, with its tags fetched (git fetch --tags).
            """;
    }

    // The nearest tag reachable from the parent of the release tag, if any. Fails
    // (and yields no previous tag) on a first tag or a root commit.
    private async Task<string?> ReadPreviousTagAsync(string tag, CancellationToken cancellationToken)
    {
        GitResult previous = await RunGitAsync(
            ["describe", "--tags", "--abbrev=0", $"{tag}^"], cancellationToken);
        return previous.ExitCode == 0 && previous.StandardOutput.Trim() is { Length: > 0 } prev
            ? prev
            : null;
    }

    // A start that is not behind the tag would give a range of commits that are not
    // in the release at all, or none, so it is refused rather than read. In a shallow
    // clone the start also has to be fetched, and so does everything between it and
    // the tag: a range cut off inside reads as a smaller release.
    private async Task<string> VerifyStartAsync(
        string since,
        string tag,
        bool shallow,
        CancellationToken cancellationToken)
    {
        GitResult verify = await RunGitAsync(
            ["rev-parse", "--verify", "--quiet", $"{since}^{{commit}}"], cancellationToken);
        if (verify.ExitCode != 0)
        {
            throw new InvalidOperationException(shallow
                ? $"""
                    The release start '{since}' is not in the fetched history of this shallow clone.
                    {FetchFullHistory}
                    """
                : $"The release start '{since}' does not resolve to a commit in the repository.");
        }

        GitResult ancestor = await RunGitAsync(["merge-base", "--is-ancestor", since, tag], cancellationToken);
        if (ancestor.ExitCode != 0)
        {
            throw new InvalidOperationException(shallow
                ? $"""
                    The release start '{since}' is not an ancestor of '{tag}' in the fetched history of this shallow clone; the commits that connect them may not have been fetched.
                    {FetchFullHistory}
                    """
                : $"The release start '{since}' is not an ancestor of '{tag}', so it cannot be where '{tag}' starts.");
        }

        if (shallow && await IsCutOffAsync(since, tag, cancellationToken))
        {
            throw new InvalidOperationException($"""
                The history between '{since}' and '{tag}' is cut off by the shallow clone, so commits of the release are missing from it.
                {FetchFullHistory}
                """);
        }

        return since;
    }

    private async Task<bool> IsShallowAsync(CancellationToken cancellationToken)
    {
        GitResult shallow = await RunGitAsync(["rev-parse", "--is-shallow-repository"], cancellationToken);
        return shallow.ExitCode == 0 && shallow.StandardOutput.Trim() == "true";
    }

    /// <summary>
    /// Whether a commit whose parents were not fetched lies inside the range. git
    /// lists those boundary commits in the <c>shallow</c> file and nowhere else;
    /// one inside the range means commits reachable from the tag but not from the
    /// start, on a merged branch, were left out.
    /// </summary>
    private async Task<bool> IsCutOffAsync(string since, string tag, CancellationToken cancellationToken)
    {
        GitResult path = await RunGitAsync(["rev-parse", "--git-path", "shallow"], cancellationToken);
        GitResult range = await RunGitAsync(["rev-list", $"{since}..{tag}"], cancellationToken);
        if (path.ExitCode != 0 || range.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to read whether '{since}..{tag}' is complete: {(path.StandardError + range.StandardError).Trim()}");
        }

        string file = Path.Combine(repositoryPath, path.StandardOutput.Trim());
        if (!File.Exists(file))
        {
            return false;
        }

        HashSet<string> boundary = [.. await File.ReadAllLinesAsync(file, cancellationToken)];
        return range.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(boundary.Contains);
    }

    /// <summary>
    /// The date the tag was created: the tagger date for an annotated tag, the
    /// commit date for a lightweight one, which is what <c>creatordate</c> means.
    /// Returns <c>null</c> when git gives nothing readable, so an output built on
    /// it can omit the field rather than invent a date.
    /// </summary>
    private async Task<DateOnly?> ReadTagDateAsync(string tag, CancellationToken cancellationToken)
    {
        GitResult date = await RunGitAsync(
            ["for-each-ref", "--format=%(creatordate:short)", $"refs/tags/{tag}"], cancellationToken);
        if (date.ExitCode != 0)
        {
            return null;
        }

        string value = date.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(string.Empty)
            .Trim();

        return DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : null;
    }

    private static List<CommitInfo> ParseCommits(string log)
    {
        List<CommitInfo> commits = [];
        foreach (string line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = line.Split(FieldSeparator, 2);
            commits.Add(new CommitInfo(fields[0], fields.Length > 1 ? fields[1] : string.Empty));
        }

        return commits;
    }

    private Task<GitResult> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
        => GitCli.RunAsync(git, repositoryPath, arguments, cancellationToken);
}
