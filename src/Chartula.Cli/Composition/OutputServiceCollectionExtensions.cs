using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for release outputs. The concrete writer (a file in the
/// working directory) is chosen here; the pipeline depends only on
/// <see cref="IChangelogJsonWriter"/>.
/// </summary>
internal static class OutputServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaOutputs(this IServiceCollection services)
    {
        services.AddSingleton<IChangelogJsonWriter>(
            _ => new FileChangelogJsonWriter(Directory.GetCurrentDirectory()));
        return services;
    }
}
