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

        Assert.True(report.HasFindings);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("3"));
    }

    [Fact]
    public void Flags_a_quoted_feature_name_absent_from_the_fact_base()
    {
        FaithfulnessReport report = _checker.Check("Introduces the `TurboSync` API.", Facts());

        Assert.True(report.HasFindings);
        Assert.Contains(report.UnsupportedClaims, c => c.Contains("TurboSync"));
    }

    // Breaking-change claims are the thorough check's job, not this one's: the
    // output is free prose, and a regex on the word cannot tell an assertion from
    // a mention. Both cases below are output this checker must leave alone.
    [Fact]
    public void Does_not_flag_prose_that_merely_mentions_breaking_changes()
    {
        FaithfulnessReport report = _checker.Check(
            "Breaking changes now float to the top when breaking-change prominence is on.",
            Facts());

        Assert.False(report.HasFindings);
    }

    [Fact]
    public void Does_not_flag_a_breaking_change_claim_the_facts_do_not_support()
    {
        FaithfulnessReport report = _checker.Check("This is a breaking change.", Facts());

        Assert.False(report.HasFindings);
    }

    [Fact]
    public void Passes_output_whose_numbers_names_and_claims_are_all_supported()
    {
        FaithfulnessReport report = _checker.Check(
            "Dark mode is here (see PR 42, closes issue 7). Ships in 1.2.0.", Facts());

        Assert.False(report.HasFindings);
        Assert.Empty(report.UnsupportedClaims);
    }

    [Fact]
    public void Does_not_flag_a_quoted_name_present_in_the_facts()
    {
        FactBase facts = Facts(new ChangeFact("feat: add the search box", 5, null,
            ChangeCategory.Feature, true, false, [], null));

        FaithfulnessReport report = _checker.Check("Adds the `search box`.", facts);

        Assert.False(report.HasFindings);
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
        Assert.False(_checker.Check(string.Empty, Facts()).HasFindings);
    }
}
