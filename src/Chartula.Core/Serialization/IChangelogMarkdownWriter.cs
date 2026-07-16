namespace Chartula.Core.Serialization;

/// <summary>
/// Writes a release section into <c>CHANGELOG.md</c>, prepending it while
/// preserving existing history. Idempotent for a given tag. The pipeline depends
/// only on this port, not on where or how the file is written.
/// </summary>
public interface IChangelogMarkdownWriter
{
    /// <summary>Writes the section for <paramref name="tag"/> and returns the path.</summary>
    Task<string> WriteAsync(string tag, string body, CancellationToken cancellationToken = default);
}
