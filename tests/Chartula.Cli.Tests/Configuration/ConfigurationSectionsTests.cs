using Chartula.Cli.Configuration;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// Each configuration section parses on its own and can be edited independently of
/// the others.
/// </summary>
public sealed class ConfigurationSectionsTests
{
    private static IConfiguration FromYaml(string yaml)
        => new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)).Build();

    [Fact]
    public void Labels_section_parses_independently()
    {
        LabelOptions labels = FromYaml(
            """
            labels:
              exclude: [wontfix]
              onlyIncludeLabeled: true
            """).GetSection(LabelOptions.SectionName).Get<LabelOptions>()!;

        Assert.Equal(["wontfix"], labels.Exclude);
        Assert.True(labels.OnlyIncludeLabeled);
    }

    [Fact]
    public void FactBase_depth_section_parses_independently()
    {
        FactBaseOptions factBase = FromYaml(
            """
            factBase:
              depth: title-only
            """).GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>()!;

        Assert.Equal(FactBaseDepth.TitleOnly, FactBaseDepthParser.Parse(factBase.Depth));
    }

    [Fact]
    public void Faithfulness_section_parses_independently()
    {
        FaithfulnessOptions faithfulness = FromYaml(
            """
            faithfulness:
              thorough: false
            """).GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>()!;

        Assert.False(faithfulness.Thorough);
    }

    [Fact]
    public void Categories_section_parses_order_names_and_breaking_prominence()
    {
        CategoryOptions categories = FromYaml(
            """
            categories:
              order: [Feature, Fix]
              names:
                Fix: Bug Fixes
              breakingProminent: false
            """).GetSection(CategoryOptions.SectionName).Get<CategoryOptions>()!;

        Assert.Equal(["Feature", "Fix"], categories.Order);
        Assert.Equal("Bug Fixes", categories.Names["Fix"]);
        Assert.False(categories.BreakingProminent);
    }

    [Fact]
    public void Editing_one_section_leaves_the_others_at_their_defaults()
    {
        // Only the faithfulness section is present.
        IConfiguration config = FromYaml(
            """
            faithfulness:
              thorough: false
            """);

        FaithfulnessOptions faithfulness =
            config.GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>()!;
        FactBaseOptions factBase =
            config.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>() ?? new FactBaseOptions();
        CategoryOptions categories =
            config.GetSection(CategoryOptions.SectionName).Get<CategoryOptions>() ?? new CategoryOptions();

        Assert.False(faithfulness.Thorough); // edited
        Assert.Null(factBase.Depth); // untouched -> default
        Assert.True(categories.BreakingProminent); // untouched -> default
    }
}
