using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class GroundedFactsFactoryTests
{
    private static ChangeFact Change(
        string title, ChangeCategory category, bool userVisible = true, bool breaking = false)
        => new(title, 1, "https://example/pull/1", category, userVisible, breaking, [], null);

    private static FactBase Facts(params ChangeFact[] changes) => new("v1.0.0", changes);

    [Fact]
    public void Orders_changes_by_the_configured_category_order()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

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

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.Contains("(breaking)", grounded.Statements[0]); // breaking first
    }

    [Fact]
    public void Keeps_category_order_when_breaking_is_not_prominent()
    {
        CategorySettings settings = new(CategorySettings.DefaultOrder, new Dictionary<ChangeCategory, string>(), breakingProminent: false);
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, settings);

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
    public void Technical_view_keeps_the_link_and_the_full_set()
    {
        FactBase facts = Facts(
            Change("feat: visible", ChangeCategory.Feature),
            Change("refactor: internal", ChangeCategory.Refactor, userVisible: false));

        GroundedFacts grounded = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.Equal(2, grounded.Statements.Count);
        Assert.Contains(grounded.Statements, s => s.Contains("https://example/pull/1"));
    }
}
