using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Labeling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// The label names are a repository's own convention, so they live in
/// <c>chartula.yaml</c>, not in the tool.
/// The composition root must pass every configured name on. A name it forgets is a
/// rule that is configured and read, but silently ignored.
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

        // A set: the test checks membership, not the order the labels were written in.
        Assert.Contains("visibility:internal", rules.InternalLabels);
        Assert.Contains("no-changelog", rules.InternalLabels);
        Assert.Contains("visibility:user-facing", rules.UserFacingLabels);
    }

    [Fact]
    public void The_action_required_label_names_reach_the_rules_from_the_configuration_file()
    {
        LabelRules rules = Rules(
            """
            labels:
              actionRequired: [needs-migration]
            """);

        Assert.Contains("needs-migration", rules.ActionRequiredLabels);
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
