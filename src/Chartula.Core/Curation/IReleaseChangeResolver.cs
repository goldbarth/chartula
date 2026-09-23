using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Curation;

/// <summary>
/// Resolves a release's commits and merged pull requests into a set of changes.
/// It never fails only because the source data is thin:
/// <list type="bullet">
/// <item>Without pull requests it falls back to commit data.</item>
/// <item>An uninformative PR title falls back to the best available source.</item>
/// </list>
/// </summary>
public interface IReleaseChangeResolver
{
    IReadOnlyList<ReleaseChange> Resolve(
        CommitRange range,
        IReadOnlyList<PullRequestInfo> pullRequests);
}
