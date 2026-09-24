using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Rendering;

/// <summary>
/// Renders the audience versions of a release from one fact base, so the technical,
/// customer and product renderings cannot contradict each other.
/// </summary>
public interface IReleaseRenderer
{
    /// <summary>
    /// Renders <paramref name="audiences"/> from <paramref name="factBase"/>, one
    /// result per requested audience.
    /// <c>null</c> renders all audiences, which is what a release needs.
    /// A run that measures one audience's wording requests only that audience and
    /// pays for one model call instead of three.
    /// </summary>
    Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default);
}
