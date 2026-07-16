using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Labeling;

namespace Chartula.Core.Tests.Labeling;

public sealed class LabelRulePolicyTests
{
    private static ReleaseChange Change(params string[] labels)
        => new("A change", null, 1, "https://example/pull/1", labels, ChangeSource.PullRequest, null);

    [Fact]
    public void Excludes_a_change_carrying_an_excluded_label_case_insensitively()
    {
        LabelRulePolicy policy = new(new LabelRules(excludedLabels: ["ignore-me"]));

        Assert.False(policy.Evaluate(Change("Ignore-Me")).Include);
    }

    [Fact]
    public void Forces_the_category_from_a_mapped_label()
    {
        LabelRulePolicy policy = new(new LabelRules(
            categoryByLabel: new Dictionary<string, ChangeCategory> { ["security"] = ChangeCategory.Fix }));

        LabelDecision decision = policy.Evaluate(Change("security"));

        Assert.True(decision.Include);
        Assert.Equal(ChangeCategory.Fix, decision.ForcedCategory);
    }

    [Fact]
    public void Only_labeled_mode_drops_unlabeled_changes_but_keeps_labeled_ones()
    {
        LabelRulePolicy policy = new(new LabelRules(onlyIncludeLabeled: true));

        Assert.False(policy.Evaluate(Change()).Include);
        Assert.True(policy.Evaluate(Change("anything")).Include);
    }

    [Fact]
    public void No_rules_includes_everything_and_forces_nothing()
    {
        LabelRulePolicy policy = new(LabelRules.None);

        LabelDecision unlabeled = policy.Evaluate(Change());
        LabelDecision labeled = policy.Evaluate(Change("whatever"));

        Assert.True(unlabeled.Include);
        Assert.Null(unlabeled.ForcedCategory);
        Assert.True(labeled.Include);
        Assert.Null(labeled.ForcedCategory);
    }

    [Fact]
    public void Exclusion_wins_over_only_labeled_and_category_mapping()
    {
        LabelRulePolicy policy = new(new LabelRules(
            excludedLabels: ["skip"],
            categoryByLabel: new Dictionary<string, ChangeCategory> { ["security"] = ChangeCategory.Fix },
            onlyIncludeLabeled: true));

        LabelDecision decision = policy.Evaluate(Change("skip", "security"));

        Assert.False(decision.Include);
        Assert.Null(decision.ForcedCategory);
    }
}
