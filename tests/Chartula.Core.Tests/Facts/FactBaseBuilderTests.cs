using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Filtering;
using Chartula.Core.History;
using Chartula.Core.Labeling;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Tests.Facts;

public sealed class FactBaseBuilderTests
{
    private static FactBaseBuilder Builder(LabelRules? labels = null, ChangeFilterRules? rules = null)
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(labels ?? LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, rules ?? ChangeFilterRules.Default);
        return new FactBaseBuilder(new ReleaseChangeResolver(), filter, categorizer, labelPolicy);
    }

    private static CommitRange Range(params (string Sha, string Subject)[] commits)
        => new("v1.0.0", "v0.9.0",
            commits.Length == 0
                ? [new CommitInfo("sha", "subject")]
                : commits.Select(c => new CommitInfo(c.Sha, c.Subject)).ToArray());

    private static PullRequestInfo Pull(int number, string title, string? body = null, params string[] labels)
        => new(number, title, body, labels, $"https://example/pull/{number}");

    [Fact]
    public void Maps_each_merged_pull_request_to_one_fact_with_curated_category_and_flags()
    {
        FactBase facts = Builder().Build(Range(), [
            Pull(7, "feat: dark mode", "Adds a theme. Closes #12", "public"),
            Pull(8, "fix: crash on start", "Fixes #34"),
        ]);

        Assert.Equal("v1.0.0", facts.Tag);
        Assert.Equal(2, facts.Changes.Count);

        ChangeFact feature = facts.Changes[0];
        Assert.Equal("feat: dark mode", feature.Title);
        Assert.Equal(7, feature.Number);
        Assert.Equal("https://example/pull/7", feature.Url);
        Assert.Equal(ChangeCategory.Feature, feature.Category);
        Assert.True(feature.IsUserVisible);
        Assert.False(feature.IsBreaking);
        Assert.Equal([12], feature.LinkedIssues);
        Assert.Equal("Adds a theme. Closes #12", feature.Description);

        Assert.Equal(ChangeCategory.Fix, facts.Changes[1].Category);
        Assert.Equal([34], facts.Changes[1].LinkedIssues);
    }

    [Fact]
    public void Excludes_filtered_out_changes_from_the_fact_base()
    {
        FactBase facts = Builder().Build(Range(), [
            Pull(1, "feat: add search"),
            Pull(2, "chore: bump deps"),
        ]);

        ChangeFact only = Assert.Single(facts.Changes);
        Assert.Equal("feat: add search", only.Title);
    }

    [Fact]
    public void Marks_non_user_facing_categories_as_not_user_visible()
    {
        FactBase facts = Builder().Build(Range(), [Pull(1, "docs: rewrite the guide")]);

        ChangeFact docs = Assert.Single(facts.Changes);
        Assert.Equal(ChangeCategory.Documentation, docs.Category);
        Assert.False(docs.IsUserVisible);
    }

    [Fact]
    public void Treats_a_breaking_change_as_user_visible_regardless_of_category()
    {
        FactBase facts = Builder().Build(Range(), [Pull(1, "refactor!: reshape the public API")]);

        ChangeFact change = Assert.Single(facts.Changes);
        Assert.Equal(ChangeCategory.Refactor, change.Category);
        Assert.True(change.IsBreaking);
        Assert.True(change.IsUserVisible);
    }

    [Fact]
    public void Applies_a_label_forced_category_and_lets_it_rescue_a_chore()
    {
        // "chore" is normally Internal (excluded); the label forces it to Fix.
        FactBaseBuilder builder = Builder(labels: new LabelRules(
            categoryByLabel: new Dictionary<string, ChangeCategory> { ["security"] = ChangeCategory.Fix }));

        FactBase facts = builder.Build(Range(), [Pull(1, "chore: rotate keys", null, "security")]);

        ChangeFact change = Assert.Single(facts.Changes);
        Assert.Equal(ChangeCategory.Fix, change.Category);
    }

    [Fact]
    public void Falls_back_to_commit_data_when_there_are_no_pull_requests()
    {
        FactBase facts = Builder().Build(
            Range(("sha1", "feat: added from a direct commit")),
            pullRequests: []);

        ChangeFact change = Assert.Single(facts.Changes);
        Assert.Equal("feat: added from a direct commit", change.Title);
        Assert.Null(change.Number);
        Assert.Null(change.Url);
        Assert.Empty(change.LinkedIssues);
        Assert.Equal(ChangeCategory.Feature, change.Category);
    }
}
