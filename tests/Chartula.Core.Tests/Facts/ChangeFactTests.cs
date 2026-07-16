using System.Text.Json;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;

namespace Chartula.Core.Tests.Facts;

public sealed class ChangeFactTests
{
    [Fact]
    public void A_pull_request_fact_survives_a_serialization_round_trip()
    {
        ChangeFact fact = new(
            Title: "Add dark mode",
            Number: 7,
            Url: "https://github.com/octo/repo/pull/7",
            Category: ChangeCategory.Feature,
            IsUserVisible: true,
            IsBreaking: false,
            LinkedIssues: [12, 34],
            Description: "Adds a dark theme.");

        ChangeFact roundTripped = RoundTrip(fact);

        Assert.Equal(fact.Title, roundTripped.Title);
        Assert.Equal(fact.Number, roundTripped.Number);
        Assert.Equal(fact.Url, roundTripped.Url);
        Assert.Equal(fact.Category, roundTripped.Category);
        Assert.Equal(fact.IsUserVisible, roundTripped.IsUserVisible);
        Assert.Equal(fact.IsBreaking, roundTripped.IsBreaking);
        Assert.Equal(fact.LinkedIssues, roundTripped.LinkedIssues);
        Assert.Equal(fact.Description, roundTripped.Description);
    }

    [Fact]
    public void A_commit_based_fact_with_no_pull_request_survives_a_round_trip()
    {
        ChangeFact fact = new(
            Title: "fix: crash on start",
            Number: null,
            Url: null,
            Category: ChangeCategory.Fix,
            IsUserVisible: true,
            IsBreaking: false,
            LinkedIssues: [],
            Description: null);

        ChangeFact roundTripped = RoundTrip(fact);

        Assert.Null(roundTripped.Number);
        Assert.Null(roundTripped.Url);
        Assert.Null(roundTripped.Description);
        Assert.Empty(roundTripped.LinkedIssues);
        Assert.Equal(fact.Category, roundTripped.Category);
    }

    [Fact]
    public void Serialization_is_stable_across_a_round_trip()
    {
        ChangeFact fact = new(
            "feat!: drop v1", 9, "https://example/pull/9",
            ChangeCategory.Feature, true, true, [1], "BREAKING CHANGE.");

        string json = JsonSerializer.Serialize(fact);

        Assert.Equal(json, JsonSerializer.Serialize(RoundTrip(fact)));
    }

    private static ChangeFact RoundTrip(ChangeFact fact)
        => JsonSerializer.Deserialize<ChangeFact>(JsonSerializer.Serialize(fact))!;
}
