using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Rendering;

/// <summary>
/// Default <see cref="IReleaseRenderer"/>. It renders each audience from the same
/// fact base by delegating to the generator, one call per audience.
/// The same fact base feeds every audience, so the renderings cannot contradict each other.
/// <para>
/// The audience order is fixed, not taken from the caller. Two runs that request the
/// same audiences make the same calls in the same order, however the request lists them.
/// </para>
/// </summary>
public sealed class ReleaseRenderer(IReleaseChangelogGenerator generator) : IReleaseRenderer
{
    private static readonly Audience[] AllAudiences =
        [Audience.Technical, Audience.Customer, Audience.Product];

    private readonly IReleaseChangelogGenerator _generator =
        generator ?? throw new ArgumentNullException(nameof(generator));

    public async Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        Audience[] wanted = audiences is null
            ? AllAudiences
            : [.. AllAudiences.Where(audiences.Contains)];

        Dictionary<Audience, ChangelogGenerationResult> renderings = [];
        foreach (Audience audience in wanted)
        {
            renderings[audience] = await _generator.GenerateAsync(factBase, audience, cancellationToken);
        }

        return renderings;
    }
}
