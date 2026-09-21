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
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("A release tag is required.", nameof(tag));
        }

        // Fail clearly if the tag does not resolve to a commit.
        GitResult verify = await RunGitAsync(
            ["rev-parse", "--verify", "--quiet", $"{tag}^{{commit}}"], cancellationToken);
        if (verify.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Tag '{tag}' does not resolve to a commit in the repository.");
        }

        // The nearest tag reachable from the parent of the release tag, if any.
        // Fails (and yields no previous tag) on the first release or a root tag.
        GitResult previous = await RunGitAsync(
            ["describe", "--tags", "--abbrev=0", $"{tag}^"], cancellationToken);
        string? fromTag = previous.ExitCode == 0 && previous.StandardOutput.Trim() is { Length: > 0 } prev
            ? prev
            : null;

        string range = fromTag is null ? tag : $"{fromTag}..{tag}";
        GitResult log = await RunGitAsync(
            ["log", "--no-color", "--pretty=format:%H%x1f%s", range], cancellationToken);
        if (log.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to read commits for '{range}': {log.StandardError.Trim()}");
        }

        DateOnly? taggedAt = await ReadTagDateAsync(tag, cancellationToken);
        return new CommitRange(tag, fromTag, ParseCommits(log.StandardOutput), taggedAt);
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
