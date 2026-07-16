using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Resolves a release's commits and merged pull requests into a set of changes,
/// degrading gracefully when PR discipline is imperfect: no pull requests falls
/// back to commit data, and a weak PR title falls back to the best available
/// source. It never fails solely because the source data is thin.
/// </summary>
public interface IReleaseChangeResolver
{
    IReadOnlyList<ReleaseChange> Resolve(
        CommitRange range,
        IReadOnlyList<PullRequestInfo> pullRequests);
}
