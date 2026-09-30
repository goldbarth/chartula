using Chartula.Cli.Commands;
using Chartula.Core.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for measurement. One sink per process: a CLI invocation is exactly
/// one run, so a singleton collects that run and nothing else.
/// Progress goes to stderr, in place only when stderr is a terminal a person watches.
/// </summary>
internal static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaObservability(this IServiceCollection services)
    {
        services.AddSingleton<IRunMetrics, RunMetrics>();
        services.AddSingleton<IRunProgress>(
            new ConsoleRunProgress(Console.Error, interactive: !Console.IsErrorRedirected));
        return services;
    }
}
