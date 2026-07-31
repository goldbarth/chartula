using Anthropic.Models.Messages;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// Thinking is billed as output tokens and models disagree about their own default,
/// so it has to be something a user can state rather than discover on an invoice.
/// </summary>
public sealed class ThinkingModeTests
{
    [Theory]
    [InlineData(null, ThinkingMode.ProviderDefault)]
    [InlineData("", ThinkingMode.ProviderDefault)]
    [InlineData("provider-default", ThinkingMode.ProviderDefault)]
    [InlineData("default", ThinkingMode.ProviderDefault)]
    [InlineData("disabled", ThinkingMode.Disabled)]
    [InlineData("off", ThinkingMode.Disabled)]
    [InlineData("Adaptive", ThinkingMode.Adaptive)]
    [InlineData("on", ThinkingMode.Adaptive)]
    public void Parses_the_configured_mode(string? configured, ThinkingMode expected)
    {
        Assert.Equal(expected, ThinkingModeParser.Parse(configured));
    }

    [Fact]
    public void Rejects_an_unknown_mode_by_name()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => ThinkingModeParser.Parse("sometimes"));

        // A typo must not fall through to a default and bill differently than asked.
        Assert.Contains("sometimes", error.Message);
        Assert.Contains("provider-default", error.Message);
    }

    [Fact]
    public void The_llm_section_carries_thinking()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(
                """
                llm:
                  thinking: disabled
                """))
            .Build();

        LlmOptions llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()!;

        Assert.Equal(ThinkingMode.Disabled, ThinkingModeParser.Parse(llm.Thinking));
    }

    // Provider default means sending no thinking field at all, not sending one that
    // says "default" - the absence is what leaves each model on its own behavior.
    [Fact]
    public void The_provider_default_adds_nothing_to_the_request()
    {
        Assert.Null(AnthropicThinking.FactoryFor(ThinkingMode.ProviderDefault));
    }

    [Theory]
    [InlineData(ThinkingMode.Disabled)]
    [InlineData(ThinkingMode.Adaptive)]
    public void An_explicit_mode_becomes_a_thinking_field_on_the_request(ThinkingMode mode)
    {
        Func<Microsoft.Extensions.AI.IChatClient, object?>? factory = AnthropicThinking.FactoryFor(mode);

        MessageCreateParams request = Assert.IsType<MessageCreateParams>(factory!(null!));

        Assert.NotNull(request.Thinking);
    }
}
