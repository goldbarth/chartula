using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class GroundedFactsFactoryTests
{
    private static ChangeFact Change(
        string title, ChangeCategory category, bool userVisible = true, bool breaking = false)
        => new(title, 1, "https://example/pull/1", category, userVisible, breaking, [], [], null);

    private static FactBase Facts(params ChangeFact[] changes) => new("v1.0.0", changes);

    [Fact]
    public void Orders_changes_by_the_configured_category_order()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default);

        // Default order puts Feature before Fix, regardless of input order.
        Assert.StartsWith("Feature", grounded.Statements[0]);
        Assert.StartsWith("Fix", grounded.Statements[1]);
    }

    [Fact]
    public void Floats_breaking_changes_to_the_top_when_prominent()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default);

        Assert.Contains("(breaking)", grounded.Statements[0]); // breaking first
    }

    [Fact]
    public void Keeps_category_order_when_breaking_is_not_prominent()
    {
        CategorySettings settings = new(CategorySettings.DefaultOrder, new Dictionary<ChangeCategory, string>(), breakingProminent: false);
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Customer, settings);

        Assert.StartsWith("Feature", grounded.Statements[0]); // feature first by category order
    }

    [Fact]
    public void Uses_the_configured_display_names()
    {
        CategorySettings settings = CategorySettings.From(
            order: null,
            names: new Dictionary<string, string> { ["Feature"] = "Features", ["Fix"] = "Bug Fixes" },
            breakingProminent: true);
        FactBase facts = Facts(Change("feat: dark mode", ChangeCategory.Feature));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Customer, settings);

        Assert.StartsWith("Features:", Assert.Single(grounded.Statements));
    }

    [Fact]
    public void Customer_view_omits_non_user_visible_changes()
    {
        FactBase facts = Facts(
            Change("feat: visible", ChangeCategory.Feature),
            Change("refactor: internal", ChangeCategory.Refactor, userVisible: false));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default);

        Assert.Single(grounded.Statements);
        Assert.Contains("feat: visible", grounded.Statements[0]);
    }

    [Fact]
    public void Technical_view_puts_each_change_in_its_common_changelog_group_in_that_order()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature),
            Change("perf: faster", ChangeCategory.Performance));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        // Changed, Added, Fixed: what a reader depends on before what is new to them.
        Assert.StartsWith("[Changed] Performance", grounded.Statements[0]);
        Assert.StartsWith("[Added] Feature", grounded.Statements[1]);
        Assert.StartsWith("[Fixed] Fix", grounded.Statements[2]);
    }

    [Fact]
    public void Technical_view_puts_a_breaking_change_first_in_its_group_not_first_in_the_release()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.StartsWith("[Added]", grounded.Statements[0]);
        Assert.StartsWith("[Fixed] Fix (breaking)", grounded.Statements[1]);
        Assert.StartsWith("[Fixed] Fix:", grounded.Statements[2]);
    }

    [Fact]
    public void Technical_view_ends_each_fact_on_the_reference_the_entry_carries()
    {
        FactBase facts = Facts(Change("feat: visible", ChangeCategory.Feature));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.EndsWith("([#1](https://example/pull/1))", Assert.Single(grounded.Statements));
    }

    [Fact]
    public void A_change_with_no_pull_request_gets_no_reference()
    {
        FactBase facts = Facts(
            new ChangeFact("feat: from a commit", null, null, ChangeCategory.Feature, true, false, [], [], null));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.Equal("[Added] Feature: feat: from a commit", Assert.Single(grounded.Statements));
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public void Technical_and_product_views_leave_out_internal_work_unless_a_label_brings_it_in(Audience audience)
    {
        FactBase facts = Facts(
            Change("chore: bump the build", ChangeCategory.Internal, userVisible: false),
            Change("refactor: labelled user-facing", ChangeCategory.Refactor, userVisible: true),
            Change("feat: labelled internal", ChangeCategory.Feature, userVisible: false));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, audience, CategorySettings.Default);

        // A label widens the categorical default and never narrows it: an internal
        // label says a user cannot meet the change, not that this reader cannot.
        Assert.Equal(2, grounded.Statements.Count);
        Assert.DoesNotContain(grounded.Statements, s => s.Contains("bump the build"));
    }

    [Fact]
    public void Product_view_puts_every_change_under_one_theme_while_no_theme_is_configured()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix: a bug", ChangeCategory.Fix));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Product, CategorySettings.Default);

        Assert.All(grounded.Statements, statement => Assert.StartsWith("[Other] ", statement));
        Assert.DoesNotContain(grounded.Statements, s => s.Contains("https://example/pull/1"));
    }
}
