using Chartula.Core.Categorization;
using Chartula.Core.Faithfulness;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Faithfulness;

public sealed class RuleBasedFaithfulnessCheckerTests
{
    private readonly RuleBasedFaithfulnessChecker _checker = new();

    private static FactBase Facts(params ChangeFact[] changes)
        => new("v1.2.0", changes.Length == 0
            ? [new ChangeFact("feat: add dark mode", 42, "https://example/pull/42",
                ChangeCategory.Feature, true, false, [7], "Adds a dark theme toggle.")]
            : changes);

    [Fact]
    public void Flags_a_number_absent_from_the_fact_base()
    {
        FaithfulnessReport report = _checker.Check("Added 3 new endpoints.", Facts());

        Assert.False(report.IsFaithful);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("3"));
    }

    [Fact]
    public void Flags_a_quoted_feature_name_absent_from_the_fact_base()
    {
        FaithfulnessReport report = _checker.Check("Introduces the `TurboSync` API.", Facts());

        Assert.False(report.IsFaithful);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("TurboSync"));
    }

    [Fact]
    public void Flags_a_breaking_claim_when_no_fact_is_breaking()
    {
        FaithfulnessReport report = _checker.Check("This is a breaking change.", Facts());

        Assert.False(report.IsFaithful);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("breaking"));
    }

    [Fact]
    public void Passes_output_whose_numbers_names_and_claims_are_all_supported()
    {
        FaithfulnessReport report = _checker.Check(
            "Dark mode is here (see PR 42, closes issue 7). Ships in 1.2.0.", Facts());

        Assert.True(report.IsFaithful);
        Assert.Empty(report.UnsupportedClaims);
    }

    [Fact]
    public void Does_not_flag_a_quoted_name_present_in_the_facts()
    {
        FactBase facts = Facts(new ChangeFact("feat: add the search box", 5, null,
            ChangeCategory.Feature, true, false, [], null));

        FaithfulnessReport report = _checker.Check("Adds the `search box`.", facts);

        Assert.True(report.IsFaithful);
    }

    [Fact]
    public void Does_not_flag_breaking_when_a_fact_is_breaking()
    {
        FactBase facts = Facts(new ChangeFact("feat!: drop the v1 endpoint", 9, null,
            ChangeCategory.Feature, true, IsBreaking: true, [], null));

        FaithfulnessReport report = _checker.Check("Breaking: the v1 endpoint is gone.", facts);

        Assert.True(report.IsFaithful);
    }

    [Fact]
    public void Runs_without_any_model_dependency()
    {
        // Constructed with no arguments: there is no IChangelogModel to call, so
        // the check costs zero tokens. It is also deterministic.
        RuleBasedFaithfulnessChecker checker = new();

        FaithfulnessReport first = checker.Check("Added 3 things.", Facts());
        FaithfulnessReport second = checker.Check("Added 3 things.", Facts());

        Assert.Equal(first.UnsupportedClaims, second.UnsupportedClaims);
    }

    [Fact]
    public void Passes_empty_output()
    {
        Assert.True(_checker.Check(string.Empty, Facts()).IsFaithful);
    }
}
