using System.Text.Json;
using System.Text.RegularExpressions;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

/// <summary>
/// The facts a run rendered from read back from its record as the fact base it came
/// from, descriptions included, so a real release can be stored and replayed later.
/// <c>changelog.json</c> is published and leaves the descriptions out (#260).
/// </summary>
public sealed class FactBaseRoundTripTests
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
    public void A_fact_base_survives_a_round_trip_through_the_run_record_unchanged()
    {
        FactBase read = RunRecordJsonSerializer.DeserializeFactBase(RunRecordJsonSerializer.SerializeFacts(Facts));

        Assert.Equal(Facts, read);
    }

    // #260: a description is what its author wrote for reviewers. The file meant to be
    // published carries every other fact, and the renderings.
    [Fact]
    public void Changelog_json_carries_no_description_and_says_so_with_its_version()
    {
        string json = ChangelogJsonSerializer.Serialize(
            Facts, new Dictionary<Audience, string> { [Audience.Customer] = "Dark mode is here." });

        using JsonDocument parsed = JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.DoesNotContain("Adds a toggle.", json);
        foreach (JsonElement change in parsed.RootElement.GetProperty("changes").EnumerateArray())
        {
            Assert.False(change.TryGetProperty("description", out _));
            Assert.True(change.TryGetProperty("title", out _));
        }
    }

    [Fact]
    public void Facts_written_before_labels_existed_still_read_with_an_empty_list()
    {
        // An optional field added later must not break an earlier file. It reads with an
        // empty label list, not with a null the rest of the pipeline would have to guard against.
        // Line endings differ per platform, so the field is removed by pattern.
        string json = Regex.Replace(
            RunRecordJsonSerializer.SerializeFacts(Facts), @"\s*""labels"":\s*\[[^\]]*\],", string.Empty);

        Assert.DoesNotContain("labels", json);
        Assert.All(RunRecordJsonSerializer.DeserializeFactBase(json).Changes, change => Assert.Empty(change.Labels));
    }

    [Fact]
    public void A_record_without_facts_is_refused_with_a_clear_message()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => RunRecordJsonSerializer.DeserializeFactBase("""{ "schemaVersion": 3, "tag": "v1.0.0" }"""));

        Assert.Contains("v1.0.0", error.Message);
        Assert.Contains("holds no facts", error.Message);
    }

    [Fact]
    public void An_unknown_category_is_refused_with_a_clear_message()
    {
        string json = RunRecordJsonSerializer.SerializeFacts(Facts).Replace("\"Feature\"", "\"Nonsense\"");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => RunRecordJsonSerializer.DeserializeFactBase(json));

        Assert.Contains("Nonsense", error.Message);
        Assert.Contains("Feature", error.Message);
    }
}
