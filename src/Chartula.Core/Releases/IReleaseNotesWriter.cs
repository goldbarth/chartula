using Chartula.Core.PullRequests;

namespace Chartula.Core.Releases;

/// <summary>
/// Writes the generated text into the hosting platform's release notes, so it
/// appears where the release actually lives.
/// Re-running for the same tag updates the existing release instead of duplicating it.
/// The pipeline depends only on this port, not on the platform API.
/// </summary>
public interface IReleaseNotesWriter
{
    /// <summary>
    /// Writes <paramref name="body"/> as the notes for the release tagged
    /// <paramref name="tag"/> in <paramref name="repository"/>.
    /// Returns a link to the release, marked when the release is a draft.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The platform API could not be reached or returned an error.
    /// </exception>
    Task<string> WriteAsync(
        RepositoryCoordinates repository,
        string tag,
        string body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms that <see cref="WriteAsync"/> would be allowed, without writing anything.
    /// Publishing is the last step of a run, after every model call, so a refusal found
    /// only there has already been paid for.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The platform refuses the write, could not be reached or returned an error.
    /// The message names the cause the same way <see cref="WriteAsync"/> would.
    /// </exception>
    Task EnsureCanWriteAsync(
        RepositoryCoordinates repository,
        string tag,
        CancellationToken cancellationToken = default);
}
