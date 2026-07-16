using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for curation. These are pure domain services with no
/// dependencies; the pipeline depends only on the interfaces.
/// </summary>
internal static class CurationServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaCuration(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseChangeResolver, ReleaseChangeResolver>();
        services.AddSingleton<IChangeCategorizer, ConventionalCommitCategorizer>();
        return services;
    }
}
