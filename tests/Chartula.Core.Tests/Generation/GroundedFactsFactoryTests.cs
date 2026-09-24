using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Generation;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

public sealed class GroundedFactsFactoryTests
{
    private static ChangeFact Change(
        string title,
        ChangeCategory category,
        bool userVisible = true,
        bool breaking = false,
        IReadOnlyList<string>? labels = null)
        => new(title, 1, "https://example/pull/1", category, userVisible, breaking, [], labels ?? [], null);

    private static FactBase Facts(params ChangeFact[] changes) => new("v1.0.0", changes);

    private static IReadOnlyList<string> Groups(RenderPlan plan) => [.. plan.Entries.Select(entry => entry.Group)];

    [Fact]
    public void Sends_every_fact_with_the_id_its_entry_comes_back_under()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix: a bug", ChangeCategory.Fix));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Product, CategorySettings.Default);

        Assert.StartsWith("[1] ", plan.Facts.Statements[0]);
        Assert.StartsWith("[2] ", plan.Facts.Statements[1]);
        Assert.Equal([1, 2], plan.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void Orders_changes_by_the_configured_category_order()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Product, CategorySettings.Default);

        // Default order puts Feature before Fix, regardless of input order.
        Assert.StartsWith("[1] Feature", plan.Facts.Statements[0]);
        Assert.StartsWith("[2] Fix", plan.Facts.Statements[1]);
    }

    [Fact]
    public void Floats_breaking_changes_to_the_top_when_prominent()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Product, CategorySettings.Default);

        Assert.Contains("(breaking)", plan.Facts.Statements[0]);
    }

    [Fact]
    public void Keeps_category_order_when_breaking_is_not_prominent()
    {
        CategorySettings settings = new(CategorySettings.DefaultOrder, new Dictionary<ChangeCategory, string>(), breakingProminent: false);
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Product, settings);

        Assert.StartsWith("[1] Feature", plan.Facts.Statements[0]);
    }

    [Fact]
    public void Uses_the_configured_display_names()
    {
        CategorySettings settings = CategorySettings.From(
            order: null,
            names: new Dictionary<string, string> { ["Feature"] = "Features", ["Fix"] = "Bug Fixes" },
            breakingProminent: true);
        FactBase facts = Facts(Change("feat: dark mode", ChangeCategory.Feature));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Customer, settings);

        Assert.StartsWith("[1] Features:", Assert.Single(plan.Facts.Statements));
    }

    [Fact]
    public void Customer_view_omits_non_user_visible_changes()
    {
        FactBase facts = Facts(
            Change("feat: visible", ChangeCategory.Feature),
            Change("refactor: internal", ChangeCategory.Refactor, userVisible: false));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default);

        Assert.Contains("feat: visible", Assert.Single(plan.Facts.Statements));
    }

    [Fact]
    public void Customer_view_puts_what_the_reader_has_to_act_on_first_then_groups_by_kind()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature),
            Change("perf: faster", ChangeCategory.Performance),
            Change("feat!: a breaking feature", ChangeCategory.Feature, breaking: true));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default);

        // The customer template's groups and order: entries that require action come
        // before all others (B1 of the customer rubric).
        Assert.Equal(["What needs action", "What's New", "What's Changed", "Bug Fixes"], Groups(plan));
        Assert.True(plan.Entries[0].IsBreaking);
    }

    [Fact]
    public void Customer_view_moves_a_change_labelled_as_asking_something_to_the_top_and_says_so()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("feat: move the label rules", ChangeCategory.Feature, labels: ["Needs-Migration"]));
        HashSet<string> actionLabels = new(["needs-migration"], StringComparer.OrdinalIgnoreCase);

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Customer, CategorySettings.Default, actionLabels);

        // Not breaking, but the reader still has to act. A category cannot tell that,
        // the PR author can. The marker tells the model there is something to do, so it
        // writes the entry's fourth part.
        Assert.Equal(["What needs action", "What's New"], Groups(plan));
        Assert.False(plan.Entries[0].IsBreaking);
        Assert.Contains("(action required)", plan.Facts.Statements[0]);
    }

    [Fact]
    public void An_action_label_changes_nothing_outside_the_customer_view()
    {
        FactBase facts = Facts(Change("feat: move the label rules", ChangeCategory.Feature, labels: ["needs-migration"]));
        HashSet<string> actionLabels = new(["needs-migration"], StringComparer.OrdinalIgnoreCase);

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default, actionLabels);

        Assert.Equal("Added", Assert.Single(plan.Entries).Group);
        Assert.DoesNotContain("(action required)", Assert.Single(plan.Facts.Statements));
    }

    [Fact]
    public void Technical_view_puts_each_change_in_its_common_changelog_group_in_that_order()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature),
            Change("perf: faster", ChangeCategory.Performance));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        // Changed, Added, Fixed: changes to what the reader depends on come before what is new.
        Assert.Equal(["Changed", "Added", "Fixed"], Groups(plan));
        Assert.StartsWith("[1] Performance", plan.Facts.Statements[0]);
    }

    [Fact]
    public void Technical_view_puts_a_breaking_change_first_in_its_group_not_first_in_the_release()
    {
        FactBase facts = Facts(
            Change("fix: a bug", ChangeCategory.Fix),
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix!: a breaking fix", ChangeCategory.Fix, breaking: true));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.Equal(["Added", "Fixed", "Fixed"], Groups(plan));
        Assert.True(plan.Entries[1].IsBreaking);
        Assert.False(plan.Entries[2].IsBreaking);
    }

    [Fact]
    public void Technical_view_plans_the_reference_the_entry_ends_on_and_does_not_send_it()
    {
        FactBase facts = Facts(Change("feat: visible", ChangeCategory.Feature));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        // Code writes the whole reference, so the model has nothing to copy or get wrong.
        Assert.Equal("([#1](https://example/pull/1))", Assert.Single(plan.Entries).Reference);
        Assert.DoesNotContain("https://", Assert.Single(plan.Facts.Statements));
    }

    [Fact]
    public void A_change_with_no_pull_request_gets_no_reference()
    {
        FactBase facts = Facts(
            new ChangeFact("feat: from a commit", null, null, ChangeCategory.Feature, true, false, [], [], null));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Technical, CategorySettings.Default);

        Assert.Null(Assert.Single(plan.Entries).Reference);
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

        RenderPlan plan = GroundedFactsFactory.Build(facts, audience, CategorySettings.Default);

        // A label widens the category-based default but never narrows it: an internal
        // label says users cannot meet the change, not that this reader cannot.
        Assert.Equal(2, plan.Entries.Count);
        Assert.DoesNotContain(plan.Facts.Statements, s => s.Contains("bump the build"));
    }

    [Fact]
    public void Product_view_puts_every_change_under_one_theme_while_no_theme_is_configured()
    {
        FactBase facts = Facts(
            Change("feat: a feature", ChangeCategory.Feature),
            Change("fix: a bug", ChangeCategory.Fix));

        RenderPlan plan = GroundedFactsFactory.Build(facts, Audience.Product, CategorySettings.Default);

        Assert.All(plan.Entries, entry => Assert.Equal("Other", entry.Group));
        Assert.All(plan.Entries, entry => Assert.Null(entry.Reference));
    }
}
