using System.Reflection;
using Chartula.Cli.Configuration;
using Chartula.Core.Review;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// The schema is listed by hand, so an option added to a type and not to the list would
/// be refused as an unknown key. This holds the two together.
/// </summary>
public sealed class ChartulaYamlSchemaTests
{
    public static TheoryData<string, Type> Sections => new()
    {
        { "llm", typeof(LlmOptions) },
        { "github", typeof(GitHubOptions) },
        { "labels", typeof(LabelOptions) },
        { "filter", typeof(FilterOptions) },
        { "factBase", typeof(FactBaseOptions) },
        { "categories", typeof(CategoryOptions) },
        { "faithfulness", typeof(FaithfulnessOptions) },
        { "review", typeof(ReviewOptions) },
    };

    [Theory]
    [MemberData(nameof(Sections))]
    public void Every_option_is_a_key_of_its_section_and_nothing_else_is(string section, Type options)
    {
        YamlKey? schema = ChartulaYamlSchema.Root.Find(section);

        Assert.NotNull(schema);
        Assert.Equal(
            options.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name.ToLowerInvariant())
                .Order(),
            schema.Keys.Select(key => key.Name.ToLowerInvariant()).Order());
    }

    [Fact]
    public void Every_section_is_covered_here()
    {
        Assert.Equal(
            Sections.Select(row => (string)row[0]).Order(),
            ChartulaYamlSchema.Root.Keys.Select(key => key.Name).Order());
    }
}
