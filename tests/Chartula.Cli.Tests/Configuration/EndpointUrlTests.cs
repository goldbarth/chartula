using Chartula.Cli.Configuration;

namespace Chartula.Cli.Tests.Configuration;

public sealed class EndpointUrlTests
{
    [Theory]
    [InlineData("https://api.github.com/")]
    [InlineData("https://192.168.1.10:11434/v1")]
    [InlineData("http://localhost:11434/v1")]
    [InlineData("http://LOCALHOST:1234/v1")]
    [InlineData("http://127.0.0.1:1234/v1")]
    [InlineData("http://127.0.0.2/")]
    [InlineData("http://[::1]:8080/")]
    public void Accepts_https_anywhere_and_http_to_this_machine(string url)
        => Assert.Equal(new Uri(url), EndpointUrl.Require("SETTING", url));

    [Theory]
    [InlineData("http://api.github.com/")]
    [InlineData("http://192.168.1.10:11434/v1")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://0.0.0.0:11434/v1")]
    [InlineData("http://localhost.example.com/")]
    public void Refuses_http_to_another_machine_naming_the_setting(string url)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => EndpointUrl.Require("SETTING", url));

        Assert.Contains("SETTING", error.Message);
        Assert.Contains(url, error.Message);
        Assert.Contains("https", error.Message);
    }

    [Theory]
    [InlineData("localhost:11434")]
    [InlineData("ftp://example.com/")]
    [InlineData("not a url")]
    [InlineData("/v1")]
    public void Refuses_what_is_not_an_http_or_https_url(string url)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => EndpointUrl.Require("SETTING", url));

        Assert.StartsWith("Invalid SETTING", error.Message);
    }
}
