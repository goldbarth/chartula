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
    /// <paramref name="since"/> when it is given, otherwise after the previous tag,
    /// up to and including <paramref name="tag"/>. With neither, returns all
    /// history up to the tag.
    /// </summary>
    /// <param name="tag">The release tag.</param>
    /// <param name="since">
    /// A tag or commit the release starts after, overriding the previous tag. It
    /// has to be an ancestor of <paramref name="tag"/>, or the range means nothing.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="System.ArgumentException"><paramref name="tag"/> is null or blank.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// The tag or the start does not resolve, the start is not an ancestor of the
    /// tag, or history could not be read.
    /// </exception>
    Task<CommitRange> ReadReleaseCommitsAsync(
        string tag,
        string? since = null,
        CancellationToken cancellationToken = default);
}
