using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Rendering;

/// <summary>
/// Renders audience versions of a release from one fact base, so the technical,
/// customer, and product renderings can never contradict each other.
/// </summary>
public interface IReleaseRenderer
{
    /// <summary>
    /// Renders <paramref name="audiences"/> from <paramref name="factBase"/>,
    /// returning one result per audience asked for. Null renders all of them,
    /// which is what a release wants; a run measuring one audience's wording asks
    /// for that one and pays for one call rather than three.
    /// </summary>
    Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default);
}
