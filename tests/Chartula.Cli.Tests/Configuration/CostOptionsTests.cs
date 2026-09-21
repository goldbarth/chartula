using Chartula.Cli.Configuration;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

public sealed class CostOptionsTests
{
    private static CostOptions Read(string yaml)
        => CostOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)).Build());

    [Fact]
    public void Nothing_configured_means_no_ceiling_and_no_price()
        => Assert.Equal(new CostOptions(null, null), Read(""));

    [Fact]
    public void Reads_a_ceiling_and_a_price()
        => Assert.Equal(
            new CostOptions(1.5m, new ModelPrice(0.5m, 2m)),
            Read("cost:\n  ceiling: 1.50\n  inputPerMillionTokens: 0.5\n  outputPerMillionTokens: 2"));

    [Theory]
    [InlineData("cost:\n  ceiling: -1")]
    [InlineData("cost:\n  ceiling: one dollar")]
    public void A_ceiling_that_is_not_an_amount_is_refused(string yaml)
        => Assert.Contains("cost.ceiling", Assert.Throws<InvalidOperationException>(() => Read(yaml)).Message);

    [Fact]
    public void Half_a_price_is_refused()
        => Assert.Contains(
            "set both or neither",
            Assert.Throws<InvalidOperationException>(() => Read("cost:\n  inputPerMillionTokens: 1")).Message);
}
