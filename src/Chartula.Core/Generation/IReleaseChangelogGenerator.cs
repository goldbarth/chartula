using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Generates a changelog for a release by rephrasing the fact base through the
/// provider interface. Makes a single, minimal call per release and depends only
/// on <see cref="IChangelogModel"/>, so generation stays provider-agnostic.
/// </summary>
public interface IReleaseChangelogGenerator
{
    Task<ChangelogGenerationResult> GenerateAsync(
        FactBase factBase,
        Audience audience,
        CancellationToken cancellationToken = default);
}
