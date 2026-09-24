namespace Chartula.Core.Serialization;

/// <summary>
/// Writes a release section at the top of <c>CHANGELOG.md</c> and keeps the existing history.
/// Idempotent for a given tag.
/// The pipeline depends only on this port, not on where or how the file is written.
/// </summary>
public interface IChangelogMarkdownWriter
{
    /// <summary>Writes the section for <paramref name="tag"/> and returns the path.</summary>
    /// <param name="tag">The release tag; the section heading carries its version.</param>
    /// <param name="taggedAt">
    /// The date the tag was created, for the section heading.
    /// <c>null</c> when it could not be read. The heading then shows only the version.
    /// </param>
    /// <param name="body">The technical rendering.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<string> WriteAsync(
        string tag, DateOnly? taggedAt, string body, CancellationToken cancellationToken = default);
}
