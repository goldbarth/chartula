namespace Chartula.Core.History;

/// <summary>
/// Finds exactly the commits belonging to a release: the range since the
/// previous tag up to the release tag. The pipeline depends only on this port,
/// not on how history is read.
/// </summary>
public interface IReleaseCommitReader
{
    /// <summary>
    /// Reads the commits belonging to <paramref name="tag"/>: everything after
    /// the previous tag up to and including <paramref name="tag"/>. With no
    /// previous tag, returns all history up to the tag (first release).
    /// </summary>
    /// <exception cref="System.ArgumentException"><paramref name="tag"/> is null or blank.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// The tag does not resolve, or history could not be read.
    /// </exception>
    Task<CommitRange> ReadReleaseCommitsAsync(
        string tag,
        CancellationToken cancellationToken = default);
}
