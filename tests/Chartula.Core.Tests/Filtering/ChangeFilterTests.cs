using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;

namespace Chartula.Core.Tests.Filtering;

public sealed class ChangeFilterTests
{
    private static ChangeFilter Filter(LabelRules? labels = null, ChangeFilterRules? rules = null)
        => new(
            new ConventionalCommitCategorizer(),
            new LabelRulePolicy(labels ?? LabelRules.None),
            rules ?? ChangeFilterRules.Default);

    private static ReleaseChange Change(string title, string? description = null, params string[] labels)
        => new(title, description, 1, "https://example/pull/1", labels, ChangeSource.PullRequest, null);

    [Theory]
    [InlineData("chore: bump deps")]
    [InlineData("ci: add runner")]
    [InlineData("build: switch tfm")]
    [InlineData("test: cover filter")]
    public void Excludes_internal_and_chore_changes_by_default(string title)
    {
        Assert.False(Filter().ShouldInclude(Change(title)));
    }

    [Theory]
    [InlineData("feat: add search")]
    [InlineData("fix: crash on start")]
    [InlineData("docs: update readme")]
    public void Keeps_user_facing_categories_by_default(string title)
    {
        Assert.True(Filter().ShouldInclude(Change(title)));
    }

    [Fact]
    public void Config_can_stop_excluding_internal()
    {
        ChangeFilter filter = Filter(rules: ChangeFilterRules.From([]));

        Assert.True(filter.ShouldInclude(Change("chore: bump deps")));
    }

    [Fact]
    public void Config_can_exclude_other_categories()
    {
        ChangeFilter filter = Filter(rules: ChangeFilterRules.From(["feature"]));

        Assert.False(filter.ShouldInclude(Change("feat: add search")));
    }

    [Fact]
    public void A_label_can_exclude_an_otherwise_included_change()
    {
        ChangeFilter filter = Filter(labels: new LabelRules(excludedLabels: ["ignore"]));

        Assert.False(filter.ShouldInclude(Change("feat: add search", labels: "ignore")));
    }

    [Fact]
    public void A_label_forced_into_an_excluded_category_is_dropped()
    {
        ChangeFilter filter = Filter(labels: new LabelRules(
            categoryByLabel: new Dictionary<string, ChangeCategory> { ["housekeeping"] = ChangeCategory.Internal }));

        Assert.False(filter.ShouldInclude(Change("feat: add search", labels: "housekeeping")));
    }

    [Fact]
    public void Only_labeled_mode_drops_unlabeled_changes()
    {
        ChangeFilter filter = Filter(labels: new LabelRules(onlyIncludeLabeled: true));

        Assert.False(filter.ShouldInclude(Change("feat: add search")));
        Assert.True(filter.ShouldInclude(Change("feat: add search", labels: "public")));
    }

    [Fact]
    public void A_breaking_change_is_never_dropped_even_when_its_category_is_excluded()
    {
        // Feature is excluded here, but the breaking marker keeps it in.
        ChangeFilter filter = Filter(rules: ChangeFilterRules.From(["feature", "internal"]));

        Assert.True(filter.ShouldInclude(Change("feat!: remove v1 endpoint")));
        Assert.True(filter.ShouldInclude(Change("chore!: drop legacy config")));
    }

    [Fact]
    public void A_label_exclusion_still_wins_over_a_breaking_change()
    {
        ChangeFilter filter = Filter(labels: new LabelRules(excludedLabels: ["skip"]));

        Assert.False(filter.ShouldInclude(Change("feat!: remove v1", labels: "skip")));
    }
}
