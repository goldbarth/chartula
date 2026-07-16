using System.Net.Http.Headers;
using Chartula.Cli.Configuration;
using Chartula.Core.PullRequests;
using Chartula.Infrastructure.PullRequests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for reading pull requests. The concrete reader (GitHub REST)
/// and its HTTP configuration are chosen here; the pipeline depends only on
/// <see cref="IReleasePullRequestReader"/>.
/// </summary>
internal static class PullRequestServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaPullRequests(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        GitHubOptions options = ReadOptions(configuration);

        services.AddSingleton(options);
        services.AddSingleton<IReleasePullRequestReader>(_ =>
            new GitHubPullRequestReader(CreateGitHubClient(options, configuration)));
        return services;
    }

    private static GitHubOptions ReadOptions(IConfiguration configuration) => new()
    {
        ApiBaseUrl = configuration[$"{GitHubOptions.SectionName}:ApiBaseUrl"] ?? "https://api.github.com/",
        TokenEnvironmentVariable =
            configuration[$"{GitHubOptions.SectionName}:TokenEnvironmentVariable"] ?? "GITHUB_TOKEN",
    };

    private static HttpClient CreateGitHubClient(GitHubOptions options, IConfiguration configuration)
    {
        HttpClient client = new() { BaseAddress = new Uri(options.ApiBaseUrl) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chartula", "0.1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        // Read the token by name; never hardcode it. Optional - unauthenticated
        // requests work for public repos, subject to lower rate limits.
        string? token = configuration[options.TokenEnvironmentVariable];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
