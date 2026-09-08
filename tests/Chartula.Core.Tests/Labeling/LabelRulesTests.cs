using Chartula.Core.Categorization;
using Chartula.Core.Labeling;

namespace Chartula.Core.Tests.Labeling;

public sealed class LabelRulesTests
{
    [Fact]
    public void From_parses_category_names_case_insensitively()
    {
        LabelRules rules = LabelRules.From(
            excludedLabels: null,
            categoryByLabel: new Dictionary<string, string> { ["security"] = "fix" },
            onlyIncludeLabeled: false);

        // Value parsed regardless of case, key matched case-insensitively.
        Assert.True(rules.CategoryByLabel.TryGetValue("SECURITY", out ChangeCategory category));
        Assert.Equal(ChangeCategory.Fix, category);
    }

    [Fact]
    public void From_throws_a_clear_error_for_an_unknown_category()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            LabelRules.From(
                excludedLabels: null,
                categoryByLabel: new Dictionary<string, string> { ["oops"] = "nonsense" },
                onlyIncludeLabeled: false));

        Assert.Contains("nonsense", ex.Message);
        Assert.Contains("oops", ex.Message);
    }

    [Fact]
    public void From_carries_the_visibility_labels_matched_case_insensitively()
    {
        LabelRules rules = LabelRules.From(
            excludedLabels: null,
            categoryByLabel: null,
            onlyIncludeLabeled: false,
            internalLabels: ["visibility:internal"],
            userFacingLabels: ["visibility:user-facing"]);

        Assert.Contains("Visibility:Internal", rules.InternalLabels);
        Assert.Contains("Visibility:User-Facing", rules.UserFacingLabels);
    }

    [Fact]
    public void None_has_no_rules()
    {
        Assert.Empty(LabelRules.None.ExcludedLabels);
        Assert.Empty(LabelRules.None.CategoryByLabel);
        Assert.False(LabelRules.None.OnlyIncludeLabeled);
        Assert.Empty(LabelRules.None.InternalLabels);
        Assert.Empty(LabelRules.None.UserFacingLabels);
    }
}
