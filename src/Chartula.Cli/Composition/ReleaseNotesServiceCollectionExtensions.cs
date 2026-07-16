using Chartula.Cli.Configuration;
using Chartula.Core.Releases;
using Chartula.Infrastructure.Releases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for writing GitHub release notes. Uses the shared
/// <see cref="GitHubHttpClientFactory"/>; the pipeline depends only on
/// <see cref="IReleaseNotesWriter"/>.
/// </summary>
internal static class ReleaseNotesServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaReleaseNotes(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        GitHubOptions options = GitHubHttpClientFactory.ReadOptions(configuration);

        services.AddSingleton<IReleaseNotesWriter>(_ =>
            new GitHubReleaseNotesWriter(GitHubHttpClientFactory.Create(options, configuration)));
        return services;
    }
}
