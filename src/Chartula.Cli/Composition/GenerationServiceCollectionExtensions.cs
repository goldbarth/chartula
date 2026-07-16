using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for generation and rendering. All of these are pure domain
/// logic over the LLM seam; the pipeline depends only on the interfaces. Requires
/// the LLM services to be registered first.
/// </summary>
internal static class GenerationServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaGeneration(this IServiceCollection services)
    {
        services.AddSingleton<IChangelogFormatter, ChangelogFormatter>();
        services.AddSingleton<IReleaseChangelogGenerator, ReleaseChangelogGenerator>();
        services.AddSingleton<IReleaseRenderer, ReleaseRenderer>();
        return services;
    }
}
