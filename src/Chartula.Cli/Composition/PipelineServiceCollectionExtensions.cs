using Chartula.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the release pipeline. The orchestrator depends only on the
/// ports registered by the other compositions; the pipeline depends only on
/// <see cref="IReleasePipeline"/>. Register this after all the step services.
/// </summary>
internal static class PipelineServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaPipeline(this IServiceCollection services)
    {
        services.AddSingleton<IReleasePipeline, ReleasePipeline>();
        return services;
    }
}
