using Chartula.Core.Serialization;
using Chartula.Infrastructure.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for release outputs. The concrete writers (files in the
/// working directory) are chosen here; the pipeline depends only on the ports.
/// </summary>
internal static class OutputServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaOutputs(this IServiceCollection services)
    {
        services.AddSingleton<IChangelogJsonWriter>(
            _ => new FileChangelogJsonWriter(Directory.GetCurrentDirectory()));
        services.AddSingleton<IChangelogMarkdownWriter>(
            _ => new FileChangelogMarkdownWriter(Directory.GetCurrentDirectory()));
        return services;
    }
}
