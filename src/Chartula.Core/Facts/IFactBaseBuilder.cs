using Chartula.Core.History;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Facts;

/// <summary>
/// Transforms a release's commits and merged pull requests into the complete fact
/// base, establishing every fact deterministically before any writing happens.
/// No LLM is involved.
/// </summary>
public interface IFactBaseBuilder
{
    FactBase Build(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests);

    /// <summary>The fact base together with the changes dropped from it, for a preview.</summary>
    CuratedRelease Curate(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests)
        => new(Build(range, pullRequests), []);
}
