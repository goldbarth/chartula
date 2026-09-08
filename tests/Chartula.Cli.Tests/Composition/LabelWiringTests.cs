using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Labeling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// The label names are a repository's own convention, so they live in
/// <c>chartula.yaml</c> rather than in the tool. This is the seam where that is
/// either true or quietly not: a name the composition root forgets to pass on is a
/// rule that is configured, read, and then ignored.
/// </summary>
public sealed class LabelWiringTests
{
    private static LabelRules Rules(string yaml)
        => new ServiceCollection()
            .AddChartulaLabelRules(new ConfigurationBuilder()
                .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml))
                .Build())
            .BuildServiceProvider()
            .GetRequiredService<LabelRules>();

    [Fact]
    public void The_visibility_label_names_reach_the_rules_from_the_configuration_file()
    {
        LabelRules rules = Rules(
            """
            labels:
              internal: [visibility:internal, no-changelog]
              userFacing: [visibility:user-facing]
            """);

        // A set, so membership is the claim - the order they were written in is not.
        Assert.Contains("visibility:internal", rules.InternalLabels);
        Assert.Contains("no-changelog", rules.InternalLabels);
        Assert.Contains("visibility:user-facing", rules.UserFacingLabels);
    }

    [Fact]
    public void A_run_that_configures_no_visibility_labels_carries_none()
    {
        LabelRules rules = Rules(
            """
            labels:
              exclude: [wontfix]
            """);

        Assert.Empty(rules.InternalLabels);
        Assert.Empty(rules.UserFacingLabels);
    }
}
