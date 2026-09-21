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
        // Resolved now rather than on first use: a machine without git is refused
        // before any work starts, and every reader runs the same binary.
        GitExecutable git = GitExecutable.FromPath();
        string checkout = Directory.GetCurrentDirectory();

        services.AddSingleton(git);
        services.AddSingleton<IReleaseCommitReader>(new GitCliCommitReader(git, checkout));
        services.AddSingleton(new GitCliRepositoryReader(git, checkout));
        return services;
    }
}
