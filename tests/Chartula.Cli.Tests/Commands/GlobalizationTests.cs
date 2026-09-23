using System.Globalization;
using System.Text.Json;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// The binary has to start where there is no ICU - a slim container image, a CI job -
/// which it did not: the runtime ended it at startup with "Couldn't find a valid ICU
/// package". What it carries is read from the runtime configuration the build writes
/// next to it, the same one the single-file binary embeds.
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

    // Otherwise the suite would check output the shipped binary does not produce.
    [Fact]
    public void The_tests_run_without_icu_too()
    {
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
    }
}
