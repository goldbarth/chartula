using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Rendering;

/// <summary>
/// Default <see cref="IReleaseRenderer"/>. It renders each audience from the same
/// fact base by delegating to the generator, one call per audience. Because the
/// same base feeds every audience, the renderings share a single source of truth
/// and cannot contradict each other.
/// </summary>
public sealed class ReleaseRenderer(IReleaseChangelogGenerator generator) : IReleaseRenderer
{
    private static readonly Audience[] AllAudiences =
        [Audience.Technical, Audience.Customer, Audience.Product];

    private readonly IReleaseChangelogGenerator _generator =
        generator ?? throw new ArgumentNullException(nameof(generator));

    public async Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAllAsync(
        FactBase factBase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        Dictionary<Audience, ChangelogGenerationResult> renderings = [];
        foreach (Audience audience in AllAudiences)
        {
            renderings[audience] = await _generator.GenerateAsync(factBase, audience, cancellationToken);
        }

        return renderings;
    }
}
