namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI with a GitHub endpoint that would carry the token in cleartext
/// to another machine. The run has to stop before its first request, which is why
/// the host does not exist: reaching it would fail differently.
/// </summary>
public sealed class InsecureEndpointTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "chartula-http-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_run_with_http_to_another_machine_stops_before_any_request()
    {
        Directory.CreateDirectory(_directory);

        (int exitCode, string error) = await CliProcess.RunChartulaAsync(
            _directory,
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "sk-test-not-used",
                ["GITHUB_TOKEN"] = "gho-test-not-used",
                ["Chartula__GitHub__ApiBaseUrl"] = "http://example.invalid/",
            },
            "preview", "--tag", "v1.0.0", "--repo", "owner/name");

        Assert.Equal(1, exitCode);
        Assert.Contains("Chartula__GitHub__ApiBaseUrl 'http://example.invalid/' uses http", error);
        Assert.DoesNotContain("Could not reach", error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
