using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Composition;

public sealed class ToolVersionTests
{
    [Fact]
    public void GitHub_requests_identify_themselves_with_the_release_version_without_build_metadata()
    {
        using HttpClient client = GitHubHttpClientFactory.Create(new GitHubOptions(), new ConfigurationBuilder().Build());

        string userAgent = client.DefaultRequestHeaders.UserAgent.ToString();
        Assert.Equal($"Chartula/{ToolVersion.Release}", userAgent);
        Assert.DoesNotContain('+', userAgent);
        Assert.StartsWith(ToolVersion.Release + "+", ToolVersion.Informational);
    }
}
