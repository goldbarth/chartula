using Chartula.Cli.Configuration;
using Chartula.Core.Facts;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Configuration;

public sealed class ChartulaYamlConfigurationTests
{
    private static IConfiguration FromYaml(string yaml)
        => new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)).Build();

    [Fact]
    public void Flatten_maps_nested_maps_and_sequences_to_prefixed_keys()
    {
        IConfiguration config = FromYaml(
            """
            llm:
              model: claude-opus-4-8
            filter:
              excludeCategories: [Internal, Documentation]
            """);

        Assert.Equal("claude-opus-4-8", config["Chartula:Llm:Model"]);
        Assert.Equal("Internal", config["Chartula:Filter:ExcludeCategories:0"]);
        Assert.Equal("Documentation", config["Chartula:Filter:ExcludeCategories:1"]);
    }

    [Fact]
    public void With_no_config_options_bind_to_their_defaults()
    {
        // No YAML, no environment: every section is absent, so options are defaults.
        IConfiguration config = new ConfigurationBuilder().Build();

        FilterOptions filter = config.GetSection(FilterOptions.SectionName).Get<FilterOptions>() ?? new FilterOptions();
        FaithfulnessOptions faithfulness =
            config.GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>() ?? new FaithfulnessOptions();
        FactBaseOptions factBase =
            config.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>() ?? new FactBaseOptions();

        Assert.Null(filter.ExcludeCategories); // default: exclude Internal only
        Assert.True(faithfulness.Thorough); // default on
        Assert.Equal(FactBaseDepthParser.Default, FactBaseDepthParser.Parse(factBase.Depth));
    }

    [Fact]
    public void A_present_config_refines_behavior()
    {
        IConfiguration config = FromYaml(
            """
            factBase:
              depth: title-description-and-issues
            faithfulness:
              thorough: false
            """);

        FactBaseOptions factBase =
            config.GetSection(FactBaseOptions.SectionName).Get<FactBaseOptions>() ?? new FactBaseOptions();
        FaithfulnessOptions faithfulness =
            config.GetSection(FaithfulnessOptions.SectionName).Get<FaithfulnessOptions>() ?? new FaithfulnessOptions();

        Assert.Equal(FactBaseDepth.TitleDescriptionAndIssues, FactBaseDepthParser.Parse(factBase.Depth));
        Assert.False(faithfulness.Thorough); // refined away from the default
    }

    [Fact]
    public void Absent_file_is_a_no_op()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "chartula-noyaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            IConfiguration config = new ConfigurationBuilder().AddChartulaYaml(emptyDir).Build();

            Assert.Empty(config.AsEnumerable());
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    [Fact]
    public void Reads_chartula_yaml_from_a_directory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "chartula-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "chartula.yaml"), "llm:\n  model: my-model\n");

            IConfiguration config = new ConfigurationBuilder().AddChartulaYaml(dir).Build();

            Assert.Equal("my-model", config["Chartula:Llm:Model"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
