using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Core.Rendering;

/// <summary>
/// Default <see cref="IReleaseRenderer"/>. It renders each audience from the same
/// fact base by delegating to the generator, one call per audience.
/// The same fact base feeds every audience, so every rendering starts from the same changes,
/// categories and breaking status.
/// <para>
/// The audience order is fixed, not taken from the caller. Two runs that request the
/// same audiences make the same calls in the same order, however the request lists them.
/// </para>
/// </summary>
/// <param name="generator">Renders one audience.</param>
/// <param name="progress">Shows each audience as its rendering starts.</param>
public sealed class ReleaseRenderer(IReleaseChangelogGenerator generator, IRunProgress? progress = null) : IReleaseRenderer
{
    private readonly IRunProgress _progress = progress ?? NullRunProgress.Instance;

    private static readonly Audience[] AllAudiences =
        [Audience.Technical, Audience.Customer, Audience.Product];

    private readonly IReleaseChangelogGenerator _generator =
        generator ?? throw new ArgumentNullException(nameof(generator));

    public IReadOnlyDictionary<Audience, RenderPlan> Plan(
        FactBase factBase, IReadOnlyCollection<Audience>? audiences = null)
    {
        ArgumentNullException.ThrowIfNull(factBase);
        return Wanted(audiences).ToDictionary(audience => audience, audience => _generator.Plan(factBase, audience));
    }

    public async Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
        FactBase factBase,
        IReadOnlyCollection<Audience>? audiences = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        Dictionary<Audience, ChangelogGenerationResult> renderings = [];
        foreach (Audience audience in Wanted(audiences))
        {
            _progress.Begin(new ProgressStep(RunStep.Rendering, audience));
            renderings[audience] = await _generator.GenerateAsync(factBase, audience, cancellationToken);
        }

        return renderings;
    }

    private static Audience[] Wanted(IReadOnlyCollection<Audience>? audiences)
        => audiences is null ? AllAudiences : [.. AllAudiences.Where(audiences.Contains)];
}
