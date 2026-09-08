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
    public void An_internal_label_says_no_reader_can_meet_the_change()
    {
        LabelRulePolicy policy = new(new LabelRules(internalLabels: ["visibility:internal"]));

        Assert.False(policy.Evaluate(Change("Visibility:Internal")).UserVisible);
    }

    [Fact]
    public void A_user_facing_label_says_a_reader_can_meet_the_change()
    {
        LabelRulePolicy policy = new(new LabelRules(userFacingLabels: ["visibility:user-facing"]));

        Assert.True(policy.Evaluate(Change("Visibility:User-Facing")).UserVisible);
    }

    [Fact]
    public void Labels_that_say_nothing_about_visibility_leave_the_answer_open()
    {
        LabelRulePolicy policy = new(new LabelRules(
            internalLabels: ["visibility:internal"],
            userFacingLabels: ["visibility:user-facing"]));

        // Not false: silence is not an answer, and the caller falls back rather than
        // treating an unlabelled change as internal.
        Assert.Null(policy.Evaluate(Change("area:cli")).UserVisible);
        Assert.Null(policy.Evaluate(Change()).UserVisible);
    }

    [Fact]
    public void An_internal_label_wins_over_a_user_facing_one_on_the_same_change()
    {
        LabelRulePolicy policy = new(new LabelRules(
            internalLabels: ["visibility:internal"],
            userFacingLabels: ["visibility:user-facing"]));

        // A contradiction a person wrote. The reading that cannot put an internal
        // change in front of a reader is the one that wins.
        Assert.False(policy.Evaluate(Change("visibility:user-facing", "visibility:internal")).UserVisible);
    }

    [Fact]
    public void Configuring_no_visibility_labels_answers_nothing()
    {
        LabelRulePolicy policy = new(LabelRules.None);

        Assert.Null(policy.Evaluate(Change("visibility:internal")).UserVisible);
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
