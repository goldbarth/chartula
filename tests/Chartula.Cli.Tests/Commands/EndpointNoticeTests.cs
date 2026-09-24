using System.Collections;
using Chartula.Cli.Commands;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Commands;

public sealed class EndpointNoticeTests
{
    private static string Notice(params (string Name, string Value)[] environment)
    {
        Hashtable variables = [];
        foreach ((string name, string value) in environment)
        {
            variables[name] = value;
        }

        return EndpointNotice.For(ChartulaConfiguration.Build(new ConfigurationBuilder(), variables));
    }

    [Fact]
    public void Names_the_default_endpoints_and_credential_variables()
    {
        string notice = Notice();

        Assert.Contains("anthropic at its default endpoint, key from ANTHROPIC_API_KEY", notice);
        Assert.Contains("https://api.github.com/, token from GITHUB_TOKEN", notice);
    }

    [Fact]
    public void Names_what_the_environment_set_and_never_a_value()
    {
        string notice = Notice(
            ("Chartula__Llm__Provider", "openai-compatible"),
            ("Chartula__Llm__Model", "qwen3:8b"),
            ("Chartula__Llm__BaseUrl", "https://api.groq.com/openai/v1"),
            ("Chartula__Llm__ApiKeyEnvironmentVariable", "GROQ_API_KEY"),
            ("GROQ_API_KEY", "gsk_secret"),
            ("GITHUB_TOKEN", "gho_secret"));

        Assert.Contains("openai-compatible at https://api.groq.com/openai/v1, key from GROQ_API_KEY", notice);
        Assert.DoesNotContain("secret", notice);
    }

    // #87: the thinking mode the run requested, in the name the provenance records, so
    // the terminal and the files agree.
    [Theory]
    [InlineData(null, "provider-default")]
    [InlineData("off", "disabled")]
    [InlineData("adaptive", "high")]
    [InlineData("xhigh", "xhigh")]
    public void Names_the_model_and_the_resolved_thinking_mode(string? configured, string resolved)
    {
        string notice = configured is null
            ? Notice(("Chartula__Llm__Model", "claude-sonnet-5"))
            : Notice(("Chartula__Llm__Model", "claude-sonnet-5"), ("Chartula__Llm__Thinking", configured));

        Assert.Contains($"claude-sonnet-5, thinking {resolved}", notice);
    }
}
