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

        string? from = string.IsNullOrWhiteSpace(since)
            ? await ReadPreviousTagAsync(tag, cancellationToken)
            : await VerifyStartAsync(since.Trim(), tag, cancellationToken);

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
    // in the release at all, or none, so it is refused rather than read.
    private async Task<string> VerifyStartAsync(string since, string tag, CancellationToken cancellationToken)
    {
        GitResult verify = await RunGitAsync(
            ["rev-parse", "--verify", "--quiet", $"{since}^{{commit}}"], cancellationToken);
        if (verify.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The release start '{since}' does not resolve to a commit in the repository.");
        }

        GitResult ancestor = await RunGitAsync(["merge-base", "--is-ancestor", since, tag], cancellationToken);
        if (ancestor.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The release start '{since}' is not an ancestor of '{tag}', so it cannot be where '{tag}' starts.");
        }

        return since;
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
