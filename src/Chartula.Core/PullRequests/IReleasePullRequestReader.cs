using Chartula.Core.History;

namespace Chartula.Core.PullRequests;

/// <summary>
/// Retrieves the merged pull requests associated with a release's commits, so the
/// changelog is summarized per PR rather than per raw commit. The pipeline
/// depends only on this port, not on the hosting platform's API.
/// </summary>
public interface IReleasePullRequestReader
{
    /// <summary>
    /// Returns the merged pull requests associated with the commits in
    /// <paramref name="range"/>, de-duplicated by number.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The platform API could not be reached or returned an error.
    /// </exception>
    Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
        RepositoryCoordinates repository,
        CommitRange range,
        CancellationToken cancellationToken = default);
}
