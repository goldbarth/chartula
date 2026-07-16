using Chartula.Core.Categorization;
using Chartula.Core.Facts;

namespace Chartula.Core.Tests.Facts;

/// <summary>
/// Facts are values: two fact bases holding the same facts are the same fact base,
/// whatever list instances happen to carry them.
/// </summary>
public sealed class FactEqualityTests
{
    private static ChangeFact Change(IReadOnlyList<int> linkedIssues)
        => new("feat: add dark mode", 42, "https://example/pull/42",
            ChangeCategory.Feature, true, false, linkedIssues, "Adds a toggle.");

    [Fact]
    public void Facts_with_the_same_linked_issues_in_different_lists_are_equal()
    {
        ChangeFact fromArray = Change([12, 13]);
        ChangeFact fromList = Change(new List<int> { 12, 13 });

        Assert.Equal(fromArray, fromList);
        Assert.Equal(fromArray.GetHashCode(), fromList.GetHashCode());
    }

    [Fact]
    public void Facts_differing_in_their_linked_issues_are_not_equal()
    {
        Assert.NotEqual(Change([12]), Change([13]));
        Assert.NotEqual(Change([12]), Change([12, 13]));
    }

    [Fact]
    public void Linked_issue_order_is_part_of_the_fact()
    {
        Assert.NotEqual(Change([12, 13]), Change([13, 12]));
    }

    [Fact]
    public void Fact_bases_holding_the_same_changes_are_equal()
    {
        FactBase fromArray = new("v1.0.0", [Change([12])]);
        FactBase fromList = new("v1.0.0", new List<ChangeFact> { Change([12]) });

        Assert.Equal(fromArray, fromList);
        Assert.Equal(fromArray.GetHashCode(), fromList.GetHashCode());
    }

    [Fact]
    public void Fact_bases_differing_in_tag_or_changes_are_not_equal()
    {
        Assert.NotEqual(new FactBase("v1.0.0", [Change([12])]), new FactBase("v1.0.1", [Change([12])]));
        Assert.NotEqual(new FactBase("v1.0.0", [Change([12])]), new FactBase("v1.0.0", []));
    }

    [Fact]
    public void An_empty_fact_base_equals_another_empty_one()
    {
        Assert.Equal(new FactBase("v1.0.0", []), new FactBase("v1.0.0", new List<ChangeFact>()));
    }
}
