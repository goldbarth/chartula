namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// Runs the built CLI from a checkout whose <c>chartula.yaml</c> is indented as in #237.
/// The run used to end in an unhandled exception with a stack trace and a core dump.
/// It must end in a configuration error that names the location.
/// </summary>
public sealed class MalformedConfigurationTests : IDisposable
{
    private readonly string _checkout = Path.Combine(Path.GetTempPath(), "chartula-malformed-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_badly_indented_file_is_a_configuration_error_without_a_stack_trace()
    {
        Directory.CreateDirectory(_checkout);
        File.WriteAllText(Path.Combine(_checkout, "chartula.yaml"),
            """
            llm:
              provider: anthropic
                model: claude-sonnet-5
                thinking: disabled
              faithfulness:
                thorough: true
            """);

        (int exitCode, string output, string error) = await CliProcess.RunChartulaAsync(
            _checkout,
            new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "sk-test-not-used" },
            "preview", "--tag", "v1.0.0", "--repo", "owner/name");

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Configuration error: chartula.yaml, line 3, column 10: this is not valid YAML", error);
        Assert.Contains("Check the indentation", error);
        Assert.DoesNotContain("Unhandled exception", error);
        Assert.DoesNotContain("   at ", error);
        Assert.Empty(output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_checkout))
        {
            Directory.Delete(_checkout, recursive: true);
        }
    }
}
