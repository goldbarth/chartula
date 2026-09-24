using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Review;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// No interactive reviewer exists, so review mode on would approve every text unseen.
/// A run that asks for review is refused instead of being given a sign-off nobody gave.
/// </summary>
public sealed class ReviewWiringTests
{
    private static ServiceProvider Build(string yaml)
        => new ServiceCollection()
            .AddChartulaReview(new ConfigurationBuilder()
                .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml))
                .Build())
            .BuildServiceProvider();

    [Fact]
    public void Review_mode_on_is_refused_naming_the_key()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Build(
            """
            review:
              enabled: true
            """));

        Assert.Contains("review.enabled", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(
        """
        review:
          enabled: false
        """)]
    public void Review_mode_off_or_absent_registers_the_coordinator(string yaml)
    {
        using ServiceProvider services = Build(yaml);

        Assert.NotNull(services.GetRequiredService<IReviewCoordinator>());
    }
}
