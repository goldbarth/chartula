using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Generates a changelog for a release by having the model rephrase the fact base.
/// It makes a single, minimal call per release.
/// It depends only on <see cref="IChangelogModel"/>, so generation stays provider-agnostic.
/// </summary>
public interface IReleaseChangelogGenerator
{
    Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default);
}
