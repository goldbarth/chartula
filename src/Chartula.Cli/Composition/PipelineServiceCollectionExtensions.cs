using Chartula.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the release pipeline.
/// <see cref="ReleasePipeline"/> depends only on the ports the other compositions register;
/// the commands depend only on <see cref="IReleasePipeline"/>.
/// Register this after all the step services.
/// </summary>
internal static class PipelineServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaPipeline(this IServiceCollection services)
    {
        services.AddSingleton<IReleasePipeline, ReleasePipeline>();
        return services;
    }
}
