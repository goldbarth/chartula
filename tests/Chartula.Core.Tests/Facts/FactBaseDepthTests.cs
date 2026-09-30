using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Filtering;
using Chartula.Core.History;
using Chartula.Core.Labeling;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Tests.Facts;

public sealed class FactBaseDepthTests
{
    private static FactBaseBuilder Builder(FactBaseDepth depth)
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, ChangeFilterRules.Default);
        return new FactBaseBuilder(new ReleaseChangeResolver(), filter, categorizer, labelPolicy, depth);
    }

    private static FactBase BuildOne(FactBaseDepth depth) => Builder(depth).Build(
        new CommitRange("v1.0.0", "v0.9.0", [new CommitInfo("sha", "subject")]),
        [new PullRequestInfo(7, "feat: dark mode", "Adds a theme. Closes #12", [], "https://example/pull/7") { CommitShas = ["sha"] }]);

    // The issue closed in the description is not read at title-only, so it is no fact there.
    [Fact]
    public void Title_only_keeps_neither_the_description_nor_the_issues_it_closes()
    {
        ChangeFact fact = Assert.Single(BuildOne(FactBaseDepth.TitleOnly).Changes);

        Assert.Equal("feat: dark mode", fact.Title);
        Assert.Null(fact.Description);
        Assert.Empty(fact.LinkedIssues);
    }

    [Fact]
    public void Title_only_keeps_an_issue_the_title_closes()
    {
        FactBase facts = Builder(FactBaseDepth.TitleOnly).Build(
            new CommitRange("v1.0.0", "v0.9.0", [new CommitInfo("sha", "subject")]),
            [new PullRequestInfo(7, "fix: crash on start, fixes #34", "Closes #12", [], "https://example/pull/7") { CommitShas = ["sha"] }]);

        Assert.Equal([34], Assert.Single(facts.Changes).LinkedIssues);
    }

    // #258: the default reads the description, so it keeps the issues it closes.
    [Fact]
    public void Title_and_description_keeps_the_description_and_the_issues_it_closes()
    {
        ChangeFact fact = Assert.Single(BuildOne(FactBaseDepth.TitleAndDescription).Changes);

        Assert.Equal("Adds a theme. Closes #12", fact.Description);
        Assert.Equal([12], fact.LinkedIssues);
    }
}
