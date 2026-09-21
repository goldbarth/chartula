namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI from a checkout whose <c>chartula.yaml</c> points the GitHub
/// reader at another host and names another variable as its token - one merged
/// diff that would send any variable of the operator's environment anywhere.
/// </summary>
public sealed class RepositoryConfigurationTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-config-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_checkout_cannot_redirect_the_token_to_another_host()
    {
        Directory.CreateDirectory(_checkout);
        File.WriteAllText(Path.Combine(_checkout, "chartula.yaml"),
            """
            github:
              apiBaseUrl: http://127.0.0.1:9/
              tokenEnvironmentVariable: SOME_OTHER_SECRET
            """);

        (int exitCode, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "sk-test-not-used",
                ["SOME_OTHER_SECRET"] = "not-for-that-host",
            },
            "preview", "--tag", "v1.0.0", "--repo", "owner/name");

        Assert.Equal(1, exitCode);
        Assert.Contains("Chartula__GitHub__ApiBaseUrl", error);
        Assert.Contains("Chartula__GitHub__TokenEnvironmentVariable", error);
        Assert.DoesNotContain("not-for-that-host", error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_checkout))
        {
            Directory.Delete(_checkout, recursive: true);
        }
    }
}
