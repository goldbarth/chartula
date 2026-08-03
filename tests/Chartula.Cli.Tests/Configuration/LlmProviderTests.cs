using Chartula.Cli.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// The provider decides where release data is sent, so a value that is not
/// understood has to stop the run rather than fall back to whatever was there
/// before.
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

        // 'openai' is the near miss to expect: it is not the dialect name, and the
        // message has to say which names exist rather than only that this one does not.
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

        // The spelling has to be one the parser accepts, or an error message would
        // name a value that does not work when pasted into chartula.yaml.
        Assert.Equal(provider, LlmProviderParser.Parse(expected));
    }

    // The property initializers on LlmOptions and the defaults table are two places
    // that state the same thing. This test is what keeps them from drifting apart.
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
