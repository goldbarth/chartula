using System.Collections;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// What a run reads from the environment: <c>Chartula__</c> settings, and the
/// credentials Chartula uses by name - never the rest of the operator's shell.
/// </summary>
public sealed class ChartulaConfigurationTests
{
    private static IConfiguration Build(string yaml, params (string Name, string Value)[] environment)
    {
        Hashtable variables = [];
        foreach ((string name, string value) in environment)
        {
            variables[name] = value;
        }

        return ChartulaConfiguration.Build(
            new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)), variables);
    }

    [Fact]
    public void A_variable_chartula_does_not_use_is_not_readable()
    {
        IConfiguration config = Build("", ("AWS_SECRET_ACCESS_KEY", "secret"), ("HOME", "/home/someone"));

        Assert.Null(config["AWS_SECRET_ACCESS_KEY"]);
        Assert.Null(config["HOME"]);
    }

    [Theory]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("OPENAI_API_KEY")]
    [InlineData("GITHUB_TOKEN")]
    public void The_default_credentials_are_readable(string name)
        => Assert.Equal("secret", Build("", (name, "secret"))[name]);

    [Theory]
    [InlineData("Chartula__Llm__ApiKeyEnvironmentVariable")]
    [InlineData("Chartula__GitHub__TokenEnvironmentVariable")]
    public void A_credential_renamed_in_the_environment_is_readable(string setting)
    {
        IConfiguration config = Build("", (setting, "GROQ_API_KEY"), ("GROQ_API_KEY", "secret"));

        Assert.Equal("secret", config["GROQ_API_KEY"]);
    }

    [Fact]
    public void A_setting_in_the_environment_overrides_the_file()
    {
        IConfiguration config = Build("llm:\n  model: from-file", ("Chartula__Llm__Model", "from-environment"));

        Assert.Equal("from-environment", config["Chartula:Llm:Model"]);
    }

    [Fact]
    public void An_endpoint_can_be_set_in_the_environment()
    {
        IConfiguration config = Build("", ("Chartula__GitHub__ApiBaseUrl", "https://ghe.example.test/api/v3/"));

        Assert.Equal("https://ghe.example.test/api/v3/", config["Chartula:GitHub:ApiBaseUrl"]);
    }
}
