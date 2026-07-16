using Chartula.Core.Facts;

namespace Chartula.Core.Serialization;

/// <summary>
/// Writes the fact base to <c>changelog.json</c> as a durable record and a source
/// for other outputs. The pipeline depends only on this port, not on where or how
/// the file is written.
/// </summary>
public interface IChangelogJsonWriter
{
    /// <summary>Writes the fact base and returns the path written to.</summary>
    Task<string> WriteAsync(FactBase factBase, CancellationToken cancellationToken = default);
}
