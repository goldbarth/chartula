using Chartula.Cli.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// The provider decides where release data is sent, so an unknown value must stop the
/// run instead of falling back to a default.
/// </summary>
public sealed class LlmProviderTests
{
    [Theory]
    [InlineData(null, LlmProvider.Anthropic)]
    [InlineData("", LlmProvider.Anthropic)]
    [InlineData("anthropic", LlmProvider.Anthropic)]
    [InlineData("Anthropic", LlmProvider.Anthropic)]
    [InlineData("openai-compatible", LlmProvider.OpenAiCompatible)]
    [InlineData("OpenAI-Compatible", LlmProvider.OpenAiCompatible)]
    [InlineData("openai_compatible", LlmProvider.OpenAiCompatible)]
    public void Parses_the_configured_provider(string? configured, LlmProvider expected)
    {
        Assert.Equal(expected, LlmProviderParser.Parse(configured));
    }

    [Fact]
    public void Rejects_an_unknown_provider_by_name()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => LlmProviderParser.Parse("openai"));

        // 'openai' is the likely near miss: it is not the dialect name. The message must
        // list the valid names, not only reject this one.
        Assert.Contains("openai", error.Message);
        Assert.Contains("anthropic", error.Message);
        Assert.Contains("openai-compatible", error.Message);
    }

    [Theory]
    [InlineData(LlmProvider.Anthropic, "anthropic")]
    [InlineData(LlmProvider.OpenAiCompatible, "openai-compatible")]
    public void Names_each_provider_the_way_configuration_spells_it(LlmProvider provider, string expected)
    {
        Assert.Equal(expected, LlmProviderParser.ToConfigurationValue(provider));

        // The parser must accept this spelling. Otherwise an error message would name a
        // value that fails when pasted into chartula.yaml.
        Assert.Equal(provider, LlmProviderParser.Parse(expected));
    }

    // The property initializers on LlmOptions and the defaults table state the same
    // defaults in two places. This test keeps them from drifting apart.
    [Fact]
    public void The_option_defaults_match_the_anthropic_defaults()
    {
        LlmOptions options = new();
        LlmProviderDefaults defaults = LlmProviderDefaults.For(LlmProvider.Anthropic);

        Assert.Equal(defaults.Model, options.Model);
        Assert.Equal(defaults.ApiKeyEnvironmentVariable, options.ApiKeyEnvironmentVariable);
        Assert.Equal(defaults.BaseUrl, options.BaseUrl);
    }
}
