using Chartula.Cli.Commands;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// A run without a GitHub token is warned before it starts, and the warning names
/// the variable the run actually reads - not the default, when the default was
/// renamed.
/// </summary>
public sealed class GitHubTokenNoticeTests
{
    private static IConfiguration Configuration(string? yaml = null, params (string Key, string? Value)[] environment)
    {
        ConfigurationBuilder builder = new();
        if (yaml is not null)
        {
            builder.AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml));
        }

        return builder
            .AddInMemoryCollection(environment.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();
    }

    [Fact]
    public void A_run_without_a_token_is_warned_with_the_cause_and_the_fix()
    {
        string? notice = GitHubTokenNotice.For(Configuration());

        Assert.NotNull(notice);
        Assert.Contains("GITHUB_TOKEN", notice);
        Assert.Contains("60", notice);
        Assert.Contains("5000", notice);
        Assert.Contains("export GITHUB_TOKEN=", notice);
    }

    [Fact]
    public void A_run_with_a_token_is_not_warned()
        => Assert.Null(GitHubTokenNotice.For(Configuration(environment: ("GITHUB_TOKEN", "gho_secret"))));

    [Fact]
    public void A_blank_token_counts_as_none()
        => Assert.NotNull(GitHubTokenNotice.For(Configuration(environment: ("GITHUB_TOKEN", "   "))));

    [Fact]
    public void The_notice_names_the_configured_variable_rather_than_the_default()
    {
        string? notice = GitHubTokenNotice.For(Configuration(
            """
            github:
              tokenEnvironmentVariable: CHARTULA_GH_TOKEN
            """));

        Assert.NotNull(notice);
        Assert.Contains("CHARTULA_GH_TOKEN", notice);
        Assert.DoesNotContain("GITHUB_TOKEN", notice);
    }

    [Fact]
    public void A_token_under_the_configured_variable_silences_the_notice()
        => Assert.Null(GitHubTokenNotice.For(Configuration(
            """
            github:
              tokenEnvironmentVariable: CHARTULA_GH_TOKEN
            """,
            ("CHARTULA_GH_TOKEN", "gho_secret"))));
}
