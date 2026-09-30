namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// #259: a preview makes no model call, so it needs no model key. Runs the built CLI
/// without one. GitHub points at a closed local port, so getting past the key shows as a
/// failed GitHub request instead of reaching the network.
/// </summary>
public sealed class PreviewWithoutKeyTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-preview-key-" + Guid.NewGuid().ToString("N"));

    private async Task CreateCheckoutAsync()
    {
        Directory.CreateDirectory(_checkout);
        foreach (string[] git in (string[][])[
                     ["init", "-b", "main"],
                     ["-c", "user.email=t@example.com", "-c", "user.name=T", "commit", "--allow-empty", "-m", "feat: A"],
                     ["tag", "v0.1.0"]])
        {
            (int gitExit, _, string gitError) = await CliProcess.RunAsync("git", _checkout, git);
            Assert.True(gitExit == 0, gitError);
        }
    }

    private Task<CliResult> RunAsync(string command)
        => CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = null,
                ["Chartula__Llm__Provider"] = null,
                ["Chartula__GitHub__ApiBaseUrl"] = "https://127.0.0.1:1/",
            },
            command, "--tag", "v0.1.0", "--repo", "owner/does-not-exist", "--yes");

    [Fact]
    public async Task A_preview_runs_without_a_model_key()
    {
        await CreateCheckoutAsync();

        (int exitCode, string output, string error) = await RunAsync("preview");

        Assert.Equal(1, exitCode);
        Assert.Contains("Could not reach the GitHub API", output);
        Assert.DoesNotContain("API key", output + error);
    }

    [Fact]
    public async Task Generate_still_needs_the_key_before_anything_is_read()
    {
        await CreateCheckoutAsync();

        (int exitCode, _, string error) = await RunAsync("generate");

        Assert.Equal(1, exitCode);
        Assert.Contains("No Anthropic API key found in ANTHROPIC_API_KEY", error);
    }

    public void Dispose() => TestDirectory.Delete(_checkout);
}
