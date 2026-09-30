using System.Collections;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Categorization;
using Chartula.Core.Filtering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// #277: <c>excludeCategories</c> distinguishes no setting, which keeps the default,
/// from an empty list, which drops no category. Reading the empty list as no setting
/// dropped internal work that the file asked to keep.
/// The tests build the configuration the way a run does, file and environment layered,
/// because the layering is where an empty value used to disappear.
/// </summary>
public sealed class FilterWiringTests
{
    private static IReadOnlySet<ChangeCategory> Excluded(string yaml, Hashtable? environment = null)
        => new ServiceCollection()
            .AddChartulaFilter(ChartulaConfiguration.Build(
                new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)),
                environment ?? []))
            .BuildServiceProvider()
            .GetRequiredService<ChangeFilterRules>()
            .ExcludedCategories;

    [Theory]
    [InlineData("")]
    [InlineData("filter:\n  excludeCategories:\n")]
    public void No_list_keeps_the_default(string yaml)
    {
        Assert.Equal([ChangeCategory.Internal], Excluded(yaml));
    }

    [Fact]
    public void An_empty_list_drops_no_category()
    {
        Assert.Empty(Excluded("filter:\n  excludeCategories: []\n"));
    }

    [Fact]
    public void An_empty_list_in_the_environment_drops_no_category()
    {
        Assert.Empty(Excluded("", new Hashtable { ["Chartula__Filter__ExcludeCategories"] = "[]" }));
    }

    [Fact]
    public void A_list_replaces_the_default()
    {
        Assert.Equal([ChangeCategory.Documentation], Excluded("filter:\n  excludeCategories: [Documentation]\n"));
    }
}
