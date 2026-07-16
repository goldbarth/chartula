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
        [new PullRequestInfo(7, "feat: dark mode", "Adds a theme. Closes #12", [], "https://example/pull/7")]);

    [Fact]
    public void Title_only_keeps_neither_description_nor_issues()
    {
        ChangeFact fact = Assert.Single(BuildOne(FactBaseDepth.TitleOnly).Changes);

        Assert.Equal("feat: dark mode", fact.Title);
        Assert.Null(fact.Description);
        Assert.Empty(fact.LinkedIssues);
    }

    [Fact]
    public void Title_and_description_keeps_the_description_but_not_issues()
    {
        ChangeFact fact = Assert.Single(BuildOne(FactBaseDepth.TitleAndDescription).Changes);

        Assert.Equal("Adds a theme. Closes #12", fact.Description);
        Assert.Empty(fact.LinkedIssues);
    }

    [Fact]
    public void Title_description_and_issues_keeps_both()
    {
        ChangeFact fact = Assert.Single(BuildOne(FactBaseDepth.TitleDescriptionAndIssues).Changes);

        Assert.Equal("Adds a theme. Closes #12", fact.Description);
        Assert.Equal([12], fact.LinkedIssues);
    }
}
