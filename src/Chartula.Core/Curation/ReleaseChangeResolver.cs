using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Default <see cref="IReleaseChangeResolver"/>. It prefers merged pull requests.
/// Without pull requests it falls back to commit data.
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

        // Merged PRs are the preferred source. Fall back to commit data only when
        // there are none.
        if (pullRequests.Count > 0)
        {
            return RevertPairing.Apply(range, pullRequests).Select(FromPullRequest).ToArray();
        }

        return range.Commits.Select(FromCommit).ToArray();
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
