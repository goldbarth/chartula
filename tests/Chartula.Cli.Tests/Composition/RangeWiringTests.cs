using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// <c>range.confirmAboveCommits</c> sets when a range is confirmed. A value that cannot be
/// read is refused, since falling back to the default would let a range run unasked.
/// </summary>
public sealed class RangeWiringTests
{
    private static ServiceProvider Build(string yaml)
        => new ServiceCollection()
            .AddChartulaPipeline(new ConfigurationBuilder()
                .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml))
                .Build())
            .BuildServiceProvider();

    [Fact]
    public void Without_a_setting_the_default_threshold_applies()
    {
        using ServiceProvider services = Build("");

        Assert.Equal(LargeRangeRule.Default, services.GetRequiredService<LargeRangeRule>());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("500", 500)]
    public void A_configured_threshold_applies(string value, int expected)
    {
        using ServiceProvider services = Build($"range:\n  confirmAboveCommits: {value}\n");

        Assert.Equal(expected, services.GetRequiredService<LargeRangeRule>().CommitThreshold);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("many")]
    [InlineData("1.5")]
    public void An_unreadable_threshold_is_refused_naming_the_key(string value)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Build($"range:\n  confirmAboveCommits: {value}\n"));

        Assert.Contains("range.confirmAboveCommits", error.Message);
    }
}
