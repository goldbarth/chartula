using Chartula.Core.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for measurement. One sink per process: a CLI invocation is exactly
/// one run, so a singleton collects that run and nothing else.
/// </summary>
internal static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaObservability(this IServiceCollection services)
    {
        services.AddSingleton<IRunMetrics, RunMetrics>();
        return services;
    }
}
