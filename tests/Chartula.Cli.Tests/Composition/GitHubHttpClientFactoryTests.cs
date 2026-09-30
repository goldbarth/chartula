using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// #272: a GitHub Enterprise URL without a trailing slash lost its last segment, so
/// every request went to the wrong path and failed with 404.
/// </summary>
public sealed class GitHubHttpClientFactoryTests
{
    private static GitHubOptions Options(string apiBaseUrl)
        => GitHubHttpClientFactory.ReadOptions(new ConfigurationBuilder()
            .AddInMemoryCollection([new($"{GitHubOptions.SectionName}:ApiBaseUrl", apiBaseUrl)])
            .Build());

    [Theory]
    [InlineData("https://github.example.com/api/v3")]
    [InlineData("https://github.example.com/api/v3/")]
    public void With_or_without_a_trailing_slash_the_requests_reach_the_same_URL(string apiBaseUrl)
    {
        GitHubOptions options = Options(apiBaseUrl);
        using HttpClient client = GitHubHttpClientFactory.Create(options, new ConfigurationBuilder().Build());

        Assert.Equal(
            "https://github.example.com/api/v3/repos/octo/repo/commits/abc/pulls",
            new Uri(client.BaseAddress!, "repos/octo/repo/commits/abc/pulls").AbsoluteUri);

        // The header line shows this value, so it names the URL the requests go to.
        Assert.Equal("https://github.example.com/api/v3/", options.ApiBaseUrl);
    }
}
