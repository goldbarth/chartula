using Chartula.Cli.Commands;
using Chartula.Cli.Terminal;
using Chartula.Core.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for measurement. One sink per process: a CLI invocation is exactly
/// one run, so a singleton collects that run and nothing else.
/// Progress goes to stderr, in place only when stderr is a terminal a person watches;
/// the caller decides that from the stream and <c>--plain</c>, and a command that shows no
/// progress, such as <c>doctor</c> building the services to check them, passes none.
/// </summary>
internal static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaObservability(this IServiceCollection services, TerminalProfile progress)
    {
        services.AddSingleton<IRunMetrics, RunMetrics>();
        services.AddSingleton<IRunProgress>(new ConsoleRunProgress(Console.Error, progress));
        return services;
    }
}
