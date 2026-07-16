using System.Net.Http.Headers;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Composition;

/// <summary>
/// Builds the configured GitHub REST <see cref="HttpClient"/> (base URL, headers,
/// bearer token by env-var name). Shared by every GitHub adapter so the HTTP setup
/// lives in one place.
/// </summary>
internal static class GitHubHttpClientFactory
{
    public static GitHubOptions ReadOptions(IConfiguration configuration) => new()
    {
        ApiBaseUrl = configuration[$"{GitHubOptions.SectionName}:ApiBaseUrl"] ?? "https://api.github.com/",
        TokenEnvironmentVariable =
            configuration[$"{GitHubOptions.SectionName}:TokenEnvironmentVariable"] ?? "GITHUB_TOKEN",
    };

    public static HttpClient Create(GitHubOptions options, IConfiguration configuration)
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
