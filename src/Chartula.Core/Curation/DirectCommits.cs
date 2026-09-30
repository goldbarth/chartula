using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// The commits of a range that belong to no merged pull request, such as a fix pushed
/// straight to the main branch.
/// They shipped with the release, so they become changes of their own next to the pull
/// requests (#257). A merge commit among them is skipped, since what it brings in is the
/// merged commits, which are in the range themselves; it is counted, so the run can say so.
/// Membership comes from <see cref="PullRequestInfo.CommitShas"/>, which the reader fills
/// from the same request per commit that finds the pull requests.
/// </summary>
/// <param name="Commits">The commits that become changes, in range order.</param>
/// <param name="SkippedMerges">The merge commits without a pull request, which do not.</param>
public sealed record DirectCommits(IReadOnlyList<CommitInfo> Commits, int SkippedMerges)
{
    public static DirectCommits Of(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(pullRequests);

        HashSet<string> inPullRequest = new(
            pullRequests.SelectMany(static pull => pull.CommitShas), StringComparer.OrdinalIgnoreCase);
        CommitInfo[] direct = [.. range.Commits.Where(commit => !inPullRequest.Contains(commit.Sha))];
        return new DirectCommits(
            [.. direct.Where(static commit => !commit.IsMerge)],
            direct.Count(static commit => commit.IsMerge));
    }

    public bool Equals(DirectCommits? other)
        => other is not null && SkippedMerges == other.SkippedMerges && Commits.SequenceEqual(other.Commits);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(SkippedMerges);
        foreach (CommitInfo commit in Commits)
        {
            hash.Add(commit);
        }

        return hash.ToHashCode();
    }
}
