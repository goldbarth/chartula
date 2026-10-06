using Chartula.Core.PullRequests;

namespace Chartula.Core.Releases;

/// <summary>
/// Writes the generated text into the hosting platform's release notes, so it
/// appears where the release actually lives.
/// Re-running for the same tag updates the existing release instead of duplicating it.
/// A release that is already published is changed only when the caller says so: its
/// notes are public, and replacing them unasked loses what someone wrote there (#334).
/// The pipeline depends only on this port, not on the platform API.
/// </summary>
public interface IReleaseNotesWriter
{
    /// <summary>
    /// Writes <paramref name="body"/> as the notes for the release tagged
    /// <paramref name="tag"/> in <paramref name="repository"/>.
    /// Returns a link to the release, marked when the release is a draft.
    /// </summary>
    /// <param name="replacePublished">Whether the notes of a published release may be replaced.</param>
    /// <exception cref="System.InvalidOperationException">
    /// The release is published and <paramref name="replacePublished"/> is not set, or the
    /// platform API could not be reached or returned an error.
    /// </exception>
    Task<string> WriteAsync(
        RepositoryCoordinates repository,
        string tag,
        string body,
        bool replacePublished,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms that <see cref="WriteAsync"/> would be allowed, without writing anything.
    /// Publishing is the last step of a run, after every model call, so a refusal found
    /// only there has already been paid for.
    /// </summary>
    /// <param name="replacePublished">Whether the notes of a published release may be replaced.</param>
    /// <exception cref="System.InvalidOperationException">
    /// The platform refuses the write, the release is published and
    /// <paramref name="replacePublished"/> is not set, or the platform could not be
    /// reached or returned an error.
    /// The message names the cause the same way <see cref="WriteAsync"/> would.
    /// </exception>
    Task EnsureCanWriteAsync(
        RepositoryCoordinates repository,
        string tag,
        bool replacePublished,
        CancellationToken cancellationToken = default);
}
