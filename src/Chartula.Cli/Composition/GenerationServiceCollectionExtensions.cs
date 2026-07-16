using Chartula.Core.Generation;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for generation. The generator is pure domain logic over the
/// LLM seam; the pipeline depends only on <see cref="IReleaseChangelogGenerator"/>.
/// Requires the LLM services to be registered first.
/// </summary>
internal static class GenerationServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaGeneration(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseChangelogGenerator, ReleaseChangelogGenerator>();
        return services;
    }
}
