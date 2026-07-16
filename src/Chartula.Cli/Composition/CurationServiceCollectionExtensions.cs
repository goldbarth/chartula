using Chartula.Core.Curation;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for curation. The resolver is pure domain logic with no
/// dependencies; the pipeline depends only on <see cref="IReleaseChangeResolver"/>.
/// </summary>
internal static class CurationServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaCuration(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseChangeResolver, ReleaseChangeResolver>();
        return services;
    }
}
