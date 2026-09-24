namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI as in #233: an OpenAI endpoint in the environment and no
/// <c>llm.provider</c>, so the provider stays <c>anthropic</c>.
/// The run must stop at config load, before the history is read and before the key
/// leaves the machine. The GitHub endpoint does not exist on purpose: reaching it would
/// fail with a different error.
/// </summary>
public sealed class ForeignProviderHostTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "chartula-host-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task An_anthropic_key_with_an_openai_endpoint_stops_the_run_before_any_request()
    {
        Directory.CreateDirectory(_directory);

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _directory,
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "sk-ant-test-not-used",
                ["GITHUB_TOKEN"] = "gho-test-not-used",
                ["Chartula__Llm__Provider"] = null,
                ["Chartula__Llm__BaseUrl"] = "https://api.openai.com/v1",
                ["Chartula__GitHub__ApiBaseUrl"] = "https://github.example.invalid/",
            },
            "preview", "--tag", "v1.0.0", "--repo", "owner/name");

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Configuration error: Chartula__Llm__BaseUrl 'https://api.openai.com/v1' is OpenAI's API", error);
        Assert.Contains("llm.provider is not set", error);
        Assert.DoesNotContain("Model:", error);
        Assert.Empty(output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
