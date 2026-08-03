using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// What the LLM section resolves to, per provider. None of these tests reach an
/// endpoint: they check the wiring the CLI does before the first request, which is
/// where a missing key or a guessed default would otherwise turn into a confusing
/// error much later.
/// </summary>
public sealed class LlmWiringTests
{
    private static ServiceProvider Build(string yaml)
        => new ServiceCollection()
            .AddChartulaLlm(Configure(yaml))
            .BuildServiceProvider();

    private static IConfiguration Configure(string yaml)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml))
            .Build();

    [Fact]
    public void With_no_llm_section_the_anthropic_defaults_apply()
    {
        LlmOptions options = Build("faithfulness:\n  thorough: true").GetRequiredService<LlmOptions>();

        Assert.Equal("anthropic", options.Provider);
        Assert.Equal("claude-opus-4-8", options.Model);
        Assert.Equal("ANTHROPIC_API_KEY", options.ApiKeyEnvironmentVariable);

        // Null, not a URL: the Anthropic client knows its own API, and repeating the
        // address here would pin a value the SDK is free to change.
        Assert.Null(options.BaseUrl);
    }

    [Fact]
    public void The_openai_compatible_provider_defaults_its_key_variable_but_not_its_endpoint()
    {
        LlmOptions options = Build(
            """
            llm:
              provider: openai-compatible
              model: qwen3:8b
              baseUrl: http://localhost:11434/v1
            """).GetRequiredService<LlmOptions>();

        Assert.Equal("openai-compatible", options.Provider);
        Assert.Equal("qwen3:8b", options.Model);
        Assert.Equal("OPENAI_API_KEY", options.ApiKeyEnvironmentVariable);
        Assert.Equal("http://localhost:11434/v1", options.BaseUrl);
    }

    // An Anthropic model id against a local server is not a smaller mistake than no
    // model at all - it is a 404 from an endpoint that never heard of it.
    [Fact]
    public void The_openai_compatible_provider_requires_a_model()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Build(
            """
            llm:
              provider: openai-compatible
              baseUrl: http://localhost:11434/v1
            """));

        Assert.Contains("llm.model", error.Message);
        Assert.Contains("openai-compatible", error.Message);
    }

    // This provider exists so release data can stay on the user's machine. A default
    // endpoint would be a hosted one, and configuring only the model would then send
    // the data off the machine without anyone saying so.
    [Fact]
    public void The_openai_compatible_provider_requires_an_endpoint()
    {
        ServiceProvider services = Build(
            """
            llm:
              provider: openai-compatible
              model: qwen3:8b
            """);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(services.GetRequiredService<IChatClient>);

        Assert.Contains("llm.baseUrl", error.Message);
    }

    [Fact]
    public void An_unusable_endpoint_is_rejected_by_name()
    {
        ServiceProvider services = Build(
            """
            llm:
              provider: openai-compatible
              model: qwen3:8b
              baseUrl: localhost:11434
            """);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(services.GetRequiredService<IChatClient>);

        Assert.Contains("localhost:11434", error.Message);
    }

    // The endpoints this provider exists for need no key at all. Refusing to build a
    // client without one would make them unreachable, so the run starts and whatever
    // the endpoint thinks of the request is the endpoint's answer to give.
    [Fact]
    public void A_missing_api_key_still_builds_a_client()
    {
        ServiceProvider services = Build(
            """
            llm:
              provider: openai-compatible
              model: qwen3:8b
              baseUrl: http://localhost:11434/v1
              apiKeyEnvironmentVariable: A_VARIABLE_THAT_IS_NOT_SET
            """);

        Assert.NotNull(services.GetRequiredService<IChatClient>());
    }

    [Fact]
    public void An_anthropic_client_is_still_built_from_the_same_section()
    {
        Assert.NotNull(Build("llm:\n  model: claude-haiku-4-5").GetRequiredService<IChatClient>());
    }

    // Thinking travels in an Anthropic request type. Dropping the setting silently
    // would bill differently than the user asked for, on a provider where the field
    // has no meaning at all.
    [Theory]
    [InlineData("adaptive")]
    [InlineData("disabled")]
    public void Thinking_is_refused_for_the_openai_compatible_provider(string thinking)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Build(
            $"""
             llm:
               provider: openai-compatible
               model: qwen3:8b
               baseUrl: http://localhost:11434/v1
               thinking: {thinking}
             """));

        Assert.Contains("llm.thinking", error.Message);
        Assert.Contains(thinking, error.Message);
    }

    [Fact]
    public void The_provider_default_is_the_one_thinking_value_that_needs_no_provider_support()
    {
        ChatModelOptions options = Build(
            """
            llm:
              provider: openai-compatible
              model: qwen3:8b
              baseUrl: http://localhost:11434/v1
              thinking: provider-default
            """).GetRequiredService<ChatModelOptions>();

        // Nothing is added to the request, which is exactly what makes it portable.
        Assert.Null(options.RawRepresentationFactory);
    }

    [Fact]
    public void An_unknown_provider_is_refused_before_anything_is_built()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => Build("llm:\n  provider: ollama"));

        Assert.Contains("ollama", error.Message);
    }
}
