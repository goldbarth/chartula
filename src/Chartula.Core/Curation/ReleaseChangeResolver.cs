using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Default <see cref="IReleaseChangeResolver"/>. Prefers merged pull requests;
/// falls back to commit data when there are none, and to the pull request body
/// (then a generic label) when a title is missing or uninformative.
/// </summary>
public sealed class ReleaseChangeResolver : IReleaseChangeResolver
{
    // Whole-title words that carry no information on their own. A prefixed title
    // like "fix: auth bug" is unaffected - only an exact match counts. This is a
    // starter set; it becomes configurable with the config work.
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

        // Merged PRs are the preferred source. Only when there are none do we
        // degrade to commit data - that is the "no clean PRs" fallback.
        if (pullRequests.Count > 0)
        {
            return pullRequests.Select(FromPullRequest).ToArray();
        }

        return range.Commits.Select(FromCommit).ToArray();
    }

    private static ReleaseChange FromPullRequest(PullRequestInfo pull) => new(
        Title: ResolveTitle(pull),
        Description: string.IsNullOrWhiteSpace(pull.Description) ? null : pull.Description,
        Number: pull.Number,
        Url: string.IsNullOrEmpty(pull.Url) ? null : pull.Url,
        Labels: pull.Labels,
        Source: ChangeSource.PullRequest,
        CommitSha: null);

    private static ReleaseChange FromCommit(CommitInfo commit) => new(
        Title: commit.Subject,
        Description: null,
        Number: null,
        Url: null,
        Labels: [],
        Source: ChangeSource.Commit,
        CommitSha: commit.Sha);

    private static string ResolveTitle(PullRequestInfo pull)
    {
        if (IsInformative(pull.Title))
        {
            return pull.Title.Trim();
        }

        string? firstBodyLine = FirstInformativeLine(pull.Description);
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
