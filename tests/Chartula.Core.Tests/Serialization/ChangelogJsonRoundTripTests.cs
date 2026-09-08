using System.Text.RegularExpressions;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

/// <summary>
/// A written changelog.json reads back as the fact base it came from. That is what lets a
/// real release be stored and replayed later.
/// </summary>
public sealed class ChangelogJsonRoundTripTests
{
    private static readonly FactBase Facts = new(
        "v1.2.0",
        [
            new ChangeFact(
                "feat: add dark mode", 42, "https://example/pull/42",
                ChangeCategory.Feature, true, false, [12, 13], ["ui"], "Adds a toggle."),
            new ChangeFact(
                "fix: handle a missing tag", null, null,
                ChangeCategory.Fix, true, true, [], [], null),
        ]);

    [Fact]
    public void A_fact_base_survives_a_round_trip_unchanged()
    {
        FactBase read = ChangelogJsonSerializer.DeserializeFactBase(ChangelogJsonSerializer.Serialize(Facts));

        Assert.Equal(Facts, read);
    }

    [Fact]
    public void The_renderings_are_dropped_because_they_are_not_facts()
    {
        string json = ChangelogJsonSerializer.Serialize(
            Facts, new Dictionary<Audience, string> { [Audience.Customer] = "Dark mode is here." });

        // The text an LLM produced must never re-enter the pipeline as a fact.
        Assert.Equal(Facts, ChangelogJsonSerializer.DeserializeFactBase(json));
    }

    [Fact]
    public void A_document_written_before_labels_existed_still_reads_at_the_same_schema_version()
    {
        // Adding an optional field does not bump schemaVersion, so a file written by an
        // earlier version has to keep reading - with no labels, not with a null the rest
        // of the pipeline would have to guard against.
        // Line endings differ per platform, so the field is cut by shape rather than
        // by an exact string.
        string json = Regex.Replace(
            ChangelogJsonSerializer.Serialize(Facts), @"\s*""labels"":\s*\[[^\]]*\],", string.Empty);

        Assert.DoesNotContain("labels", json);

        FactBase read = ChangelogJsonSerializer.DeserializeFactBase(json);

        Assert.All(read.Changes, change => Assert.Empty(change.Labels));
    }

    [Fact]
    public void An_unreadable_schema_version_is_refused_with_a_clear_message()
    {
        string json = ChangelogJsonSerializer.Serialize(Facts).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ChangelogJsonSerializer.DeserializeFactBase(json));

        Assert.Contains("99", error.Message);
        Assert.Contains("1", error.Message);
    }

    [Fact]
    public void An_unknown_category_is_refused_with_a_clear_message()
    {
        string json = ChangelogJsonSerializer.Serialize(Facts).Replace("\"Feature\"", "\"Nonsense\"");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ChangelogJsonSerializer.DeserializeFactBase(json));

        Assert.Contains("Nonsense", error.Message);
        Assert.Contains("Feature", error.Message);
    }
}
