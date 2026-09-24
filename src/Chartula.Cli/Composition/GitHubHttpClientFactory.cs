using System.Net.Http.Headers;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Composition;

/// <summary>
/// Builds the configured GitHub REST <see cref="HttpClient"/>: base URL, headers, and
/// the bearer token read from the named environment variable.
/// Every GitHub adapter uses it, so the HTTP setup lives in one place.
/// </summary>
internal static class GitHubHttpClientFactory
{
    public static GitHubOptions ReadOptions(IConfiguration configuration)
    {
        string apiBaseUrl = configuration[$"{GitHubOptions.SectionName}:ApiBaseUrl"] ?? "https://api.github.com/";
        EndpointUrl.Require(GitHubOptions.ApiBaseUrlVariable, apiBaseUrl);

        return new GitHubOptions
        {
            ApiBaseUrl = apiBaseUrl,
            TokenEnvironmentVariable =
                configuration[$"{GitHubOptions.SectionName}:TokenEnvironmentVariable"] ?? "GITHUB_TOKEN",
        };
    }

    public static HttpClient Create(GitHubOptions options, IConfiguration configuration)
    {
        HttpClient client = new() { BaseAddress = new Uri(options.ApiBaseUrl) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chartula", ToolVersion.Release));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        // Read the token by variable name, never hardcode it. The token is optional:
        // unauthenticated requests work for public repositories, with lower rate limits.
        string? token = configuration[options.TokenEnvironmentVariable];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
