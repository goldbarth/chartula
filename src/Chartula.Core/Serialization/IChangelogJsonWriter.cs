using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Serialization;

/// <summary>
/// Writes the fact base and the rendered audience texts to <c>changelog.json</c>
/// as a durable record and a source for other outputs. The pipeline depends only
/// on this port, not on where or how the file is written.
/// </summary>
public interface IChangelogJsonWriter
{
    /// <summary>
    /// Writes the fact base and any rendered audience texts, returning the path.
    /// </summary>
    Task<string> WriteAsync(
        FactBase factBase,
        IReadOnlyDictionary<Audience, string>? renderings = null,
        CancellationToken cancellationToken = default);
}
