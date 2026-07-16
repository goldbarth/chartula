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
}
