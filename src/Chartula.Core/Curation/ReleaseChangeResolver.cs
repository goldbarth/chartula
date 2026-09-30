using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Default <see cref="IReleaseChangeResolver"/>. It prefers merged pull requests.
/// A commit that belongs to none, such as a direct push, becomes a change from its
/// commit data, so a release with pull requests does not lose it (#257).
/// When a title is missing or uninformative, it falls back to the first informative
/// line of the pull request body, then to "PR #N".
/// </summary>
public sealed class ReleaseChangeResolver : IReleaseChangeResolver
{
    // Titles that carry no information on their own. Only an exact match of the whole
    // title counts, so a prefixed title like "fix: auth bug" is unaffected.
    private static readonly HashSet<string> UninformativeTitles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "wip", "update", "updates", "misc", "changes", "fix", "fixes", "cleanup",
        };

    public IReadOnlyList<ReleaseChange> Resolve(
        CommitRange range,
        IReadOnlyList<PullRequestInfo> pullRequests)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(pullRequests);

        IReadOnlyList<PullRequestInfo> kept = RevertPairing.Apply(range, pullRequests);
        HashSet<CommitInfo> direct = [.. DirectCommits.Of(range, pullRequests).Commits];

        // In range order: a pull request where its first commit is, a direct commit where
        // it is. A release with pull requests only keeps the order it always had.
        Dictionary<string, PullRequestInfo> pullByCommit = new(StringComparer.OrdinalIgnoreCase);
        foreach (PullRequestInfo pull in kept)
        {
            foreach (string sha in pull.CommitShas)
            {
                pullByCommit.TryAdd(sha, pull);
            }
        }

        List<ReleaseChange> changes = [];
        HashSet<int> placed = [];
        foreach (CommitInfo commit in range.Commits)
        {
            if (pullByCommit.TryGetValue(commit.Sha, out PullRequestInfo? pull))
            {
                if (placed.Add(pull.Number))
                {
                    changes.Add(FromPullRequest(pull));
                }
            }
            else if (direct.Contains(commit))
            {
                changes.Add(FromCommit(commit));
            }
        }

        // A pull request whose commits the source did not name has no place in the range.
        changes.AddRange(kept.Where(pull => !placed.Contains(pull.Number)).Select(FromPullRequest));
        return changes;
    }

    private static ReleaseChange FromPullRequest(PullRequestInfo pull)
    {
        // Extract the description once, here, so everything downstream sees the same
        // description: the title fallback, breaking status, linked issues and the prompt.
        string? description = PullRequestBody.Description(pull.Description);
        return new ReleaseChange(
            Title: ResolveTitle(pull, description),
            Description: description,
            Number: pull.Number,
            Url: string.IsNullOrEmpty(pull.Url) ? null : pull.Url,
            Labels: pull.Labels,
            Source: ChangeSource.PullRequest,
            CommitSha: null);
    }

    private static ReleaseChange FromCommit(CommitInfo commit) => new(
        Title: commit.Subject,
        Description: null,
        Number: null,
        Url: null,
        Labels: [],
        Source: ChangeSource.Commit,
        CommitSha: commit.Sha);

    private static string ResolveTitle(PullRequestInfo pull, string? description)
    {
        if (IsInformative(pull.Title))
        {
            return pull.Title.Trim();
        }

        string? firstBodyLine = FirstInformativeLine(description);
        return firstBodyLine ?? $"PR #{pull.Number}";
    }

    private static string? FirstInformativeLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (IsInformative(trimmed))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static bool IsInformative(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        string trimmed = title.Trim();
        if (trimmed.StartsWith("Merge ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !UninformativeTitles.Contains(trimmed);
    }
}
