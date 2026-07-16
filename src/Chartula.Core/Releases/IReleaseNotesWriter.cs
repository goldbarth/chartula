using Chartula.Core.PullRequests;

namespace Chartula.Core.Releases;

/// <summary>
/// Writes the generated text into the hosting platform's release notes, so it
/// appears where the release actually lives. Re-running for the same tag updates
/// the existing release rather than duplicating it. The pipeline depends only on
/// this port, not on the platform API.
/// </summary>
public interface IReleaseNotesWriter
{
    /// <summary>
    /// Writes <paramref name="body"/> as the notes for the release tagged
    /// <paramref name="tag"/> in <paramref name="repository"/>, and returns a link
    /// to the release.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The platform API could not be reached or returned an error.
    /// </exception>
    Task<string> WriteAsync(
        RepositoryCoordinates repository,
        string tag,
        string body,
        CancellationToken cancellationToken = default);
}
