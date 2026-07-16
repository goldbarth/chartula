using Chartula.Core.History;
using Chartula.Infrastructure.History;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for reading release history. The concrete reader (git CLI)
/// is chosen here; the pipeline depends only on <see cref="IReleaseCommitReader"/>.
/// </summary>
internal static class HistoryServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaHistory(this IServiceCollection services)
    {
        services.AddSingleton<IReleaseCommitReader>(
            _ => new GitCliCommitReader(Directory.GetCurrentDirectory()));
        return services;
    }
}
