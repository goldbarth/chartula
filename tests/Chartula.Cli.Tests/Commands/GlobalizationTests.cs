using System.Globalization;
using System.Text.Json;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// The binary must start where there is no ICU, for example in a slim container image
/// or a CI job. It used to fail at startup with "Couldn't find a valid ICU package".
/// The test reads the setting from the runtime configuration the build writes next to
/// the binary, the same file the single-file binary embeds.
/// </summary>
public sealed class GlobalizationTests
{
    [Fact]
    public void The_binary_runs_without_icu()
    {
        using JsonDocument config = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "chartula.runtimeconfig.json")));

        Assert.True(config.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("configProperties")
            .GetProperty("System.Globalization.Invariant")
            .GetBoolean());
    }

    // The tests must run invariant too. Otherwise they would check output the shipped
    // binary does not produce.
    [Fact]
    public void The_tests_run_without_icu_too()
    {
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
    }
}
