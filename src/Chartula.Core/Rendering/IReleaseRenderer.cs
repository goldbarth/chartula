using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Rendering;

/// <summary>
/// Renders every audience version of a release from one fact base, so the
/// technical, customer, and product renderings can never contradict each other.
/// </summary>
public interface IReleaseRenderer
{
    /// <summary>
    /// Renders all audiences from <paramref name="factBase"/>, returning one
    /// result per <see cref="Audience"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAllAsync(
        FactBase factBase,
        CancellationToken cancellationToken = default);
}
