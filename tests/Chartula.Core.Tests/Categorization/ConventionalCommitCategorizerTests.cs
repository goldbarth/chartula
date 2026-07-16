using Chartula.Core.Categorization;
using Chartula.Core.Curation;

namespace Chartula.Core.Tests.Categorization;

public sealed class ConventionalCommitCategorizerTests
{
    private readonly ConventionalCommitCategorizer _categorizer = new();

    private static ReleaseChange Change(string title, string? description = null)
        => new(title, description, null, null, [], ChangeSource.Commit, "sha");

    [Theory]
    [InlineData("feat: add search", ChangeCategory.Feature)]
    [InlineData("feature: add search", ChangeCategory.Feature)]
    [InlineData("fix: crash on start", ChangeCategory.Fix)]
    [InlineData("perf: cache results", ChangeCategory.Performance)]
    [InlineData("docs: update readme", ChangeCategory.Documentation)]
    [InlineData("refactor: extract helper", ChangeCategory.Refactor)]
    [InlineData("chore: bump deps", ChangeCategory.Internal)]
    [InlineData("ci: add windows runner", ChangeCategory.Internal)]
    [InlineData("build: switch to net10", ChangeCategory.Internal)]
    [InlineData("test: cover resolver", ChangeCategory.Internal)]
    public void Detects_the_category_from_the_conventional_prefix(string title, ChangeCategory expected)
    {
        ChangeClassification result = _categorizer.Classify(Change(title));

        Assert.Equal(expected, result.Category);
        Assert.False(result.IsBreaking);
    }

    [Fact]
    public void Detects_the_type_when_a_scope_is_present()
    {
        Assert.Equal(ChangeCategory.Feature, _categorizer.Classify(Change("feat(ui): dark mode")).Category);
    }

    [Fact]
    public void Is_case_insensitive_on_the_type()
    {
        Assert.Equal(ChangeCategory.Fix, _categorizer.Classify(Change("Fix: typo")).Category);
    }

    [Theory]
    [InlineData("feat!: drop v1 endpoint", ChangeCategory.Feature)]
    [InlineData("fix!: change return type", ChangeCategory.Fix)]
    public void Flags_the_bang_marker_as_breaking_without_changing_the_category(
        string title, ChangeCategory expected)
    {
        ChangeClassification result = _categorizer.Classify(Change(title));

        Assert.Equal(expected, result.Category);
        Assert.True(result.IsBreaking);
    }

    [Fact]
    public void Flags_a_breaking_change_note_in_the_body()
    {
        ChangeClassification result = _categorizer.Classify(
            Change("feat: new auth flow", "Details.\n\nBREAKING CHANGE: tokens now expire."));

        Assert.Equal(ChangeCategory.Feature, result.Category);
        Assert.True(result.IsBreaking);
    }

    [Fact]
    public void Treats_a_breaking_type_prefix_as_breaking()
    {
        ChangeClassification result = _categorizer.Classify(Change("breaking: remove legacy API"));

        Assert.True(result.IsBreaking);
    }

    [Theory]
    [InlineData("Add search to the sidebar")]
    [InlineData("Merge pull request #7")]
    [InlineData("random subject without a prefix")]
    public void Falls_back_to_Other_for_unrecognized_titles(string title)
    {
        ChangeClassification result = _categorizer.Classify(Change(title));

        Assert.Equal(ChangeCategory.Other, result.Category);
        Assert.False(result.IsBreaking);
    }

    [Fact]
    public void Is_deterministic_for_the_same_input()
    {
        ReleaseChange change = Change("feat!: drop v1", "BREAKING CHANGE: gone");

        Assert.Equal(_categorizer.Classify(change), _categorizer.Classify(change));
    }
}
