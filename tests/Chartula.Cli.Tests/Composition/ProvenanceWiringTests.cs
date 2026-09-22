using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Facts;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;

namespace Chartula.Cli.Tests.Composition;

public sealed class ProvenanceWiringTests
{
    [Fact]
    public void A_run_records_the_tool_version_the_configured_model_and_the_prompt_hash()
    {
        RunProvenance provenance = OutputServiceCollectionExtensions.Provenance(
            new LlmOptions { Provider = "openai-compatible", Model = "qwen3:8b", BaseUrl = "http://localhost:11434/v1" },
            thoroughCheck: true,
            FactBaseDepth.TitleAndDescription);

        Assert.False(string.IsNullOrWhiteSpace(provenance.ToolVersion));
        Assert.Equal("openai-compatible", provenance.Provider);
        Assert.Equal("qwen3:8b", provenance.Model);
        Assert.Equal(ChangelogPromptBuilder.PromptHash, provenance.PromptHash);
    }

    // #223: the settings that move a run's cost, in the spelling the configuration
    // uses, so a file answers what a measurement was taken with.
    [Fact]
    public void A_run_records_thinking_the_thorough_check_and_the_fact_base_depth()
    {
        RunProvenance provenance = OutputServiceCollectionExtensions.Provenance(
            new LlmOptions { Model = "claude-sonnet-5", Thinking = "off" },
            thoroughCheck: false,
            FactBaseDepth.TitleOnly);

        Assert.Equal("disabled", provenance.Thinking);
        Assert.False(provenance.ThoroughCheck);
        Assert.Equal("title-only", provenance.FactBaseDepth);
    }

    // Unset is recorded as what it means, not left out: the model's own behavior.
    [Fact]
    public void An_unset_thinking_mode_is_recorded_as_the_provider_default()
    {
        RunProvenance provenance = OutputServiceCollectionExtensions.Provenance(
            new LlmOptions { Model = "claude-sonnet-5" }, thoroughCheck: true, FactBaseDepthParser.Default);

        Assert.Equal("provider-default", provenance.Thinking);
        Assert.Equal("title-and-description", provenance.FactBaseDepth);
    }

    [Theory]
    [InlineData(ThinkingMode.ProviderDefault)]
    [InlineData(ThinkingMode.Disabled)]
    [InlineData(ThinkingMode.Adaptive)]
    public void A_recorded_thinking_mode_reads_back_as_itself(ThinkingMode mode)
        => Assert.Equal(mode, ThinkingModeParser.Parse(ThinkingModeParser.Name(mode)));

    [Theory]
    [InlineData(FactBaseDepth.TitleOnly)]
    [InlineData(FactBaseDepth.TitleAndDescription)]
    [InlineData(FactBaseDepth.TitleDescriptionAndIssues)]
    public void A_recorded_depth_reads_back_as_itself(FactBaseDepth depth)
        => Assert.Equal(depth, FactBaseDepthParser.Parse(FactBaseDepthParser.Name(depth)));
}
