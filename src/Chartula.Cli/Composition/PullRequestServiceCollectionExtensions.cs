using Chartula.Cli.Configuration;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.PullRequests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for reading pull requests. The concrete reader (GitHub REST)
/// is chosen here; the pipeline depends only on <see cref="IReleasePullRequestReader"/>.
/// The HTTP client is built by <see cref="GitHubHttpClientFactory"/>.
/// </summary>
internal static class PullRequestServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaPullRequests(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        GitHubOptions options = GitHubHttpClientFactory.ReadOptions(configuration);

        services.AddSingleton(options);
        services.AddSingleton<IReleasePullRequestReader>(_ =>
            new GitHubPullRequestReader(GitHubHttpClientFactory.Create(options, configuration)));
        return services;
    }
}
