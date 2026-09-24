using System.Text.Json;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

public sealed class ProvenanceSerializationTests
{
    private static readonly FactBase Facts = new(
        "v1.0.0",
        [new ChangeFact("feat: dark mode", 7, "https://example/pull/7", ChangeCategory.Feature, true, false, [], [], null)]);

    [Fact]
    public void Records_the_version_provider_model_and_prompt_hash()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(
            Facts, null, new RunProvenance("0.1.0-alpha+abc", "anthropic", "claude-sonnet-5", "sha256:ff")));

        JsonElement provenance = parsed.RootElement.GetProperty("provenance");
        Assert.Equal("0.1.0-alpha+abc", provenance.GetProperty("toolVersion").GetString());
        Assert.Equal("anthropic", provenance.GetProperty("provider").GetString());
        Assert.Equal("claude-sonnet-5", provenance.GetProperty("model").GetString());
        Assert.Equal("sha256:ff", provenance.GetProperty("promptHash").GetString());
    }

    // #223: false is a value, not an absence. "The check was off" must be written,
    // while an unset setting is left out like any other.
    [Fact]
    public void Records_thinking_the_thorough_check_and_the_depth_and_reads_them_back()
    {
        string json = ChangelogJsonSerializer.Serialize(
            Facts, null, new RunProvenance("0.1.0", "anthropic", "m", null, "disabled", false, "title-only"));

        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement provenance = parsed.RootElement.GetProperty("provenance");
        Assert.Equal("disabled", provenance.GetProperty("thinking").GetString());
        Assert.False(provenance.GetProperty("thoroughCheck").GetBoolean());
        Assert.Equal("title-only", provenance.GetProperty("factBaseDepth").GetString());

        Assert.Equal(
            new ChangelogProvenance("0.1.0", "anthropic", "m", null, "disabled", false, "title-only"),
            ChangelogJsonSerializer.Deserialize(json).Provenance);
    }

    [Fact]
    public void Settings_a_run_does_not_have_are_left_out()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(
            Facts, null, new RunProvenance("0.1.0", "anthropic", "m", null)));

        JsonElement provenance = parsed.RootElement.GetProperty("provenance");
        Assert.False(provenance.TryGetProperty("thinking", out _));
        Assert.False(provenance.TryGetProperty("thoroughCheck", out _));
        Assert.False(provenance.TryGetProperty("factBaseDepth", out _));
    }

    // An absent source produces an absent field. An empty field would read as a fact about the run.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_value_the_run_does_not_have_is_left_out(string? model)
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(
            Facts, null, new RunProvenance("0.1.0", "anthropic", model, "sha256:ff")));

        Assert.False(parsed.RootElement.GetProperty("provenance").TryGetProperty("model", out _));
    }

    [Fact]
    public void A_file_written_without_provenance_has_none()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Facts));

        Assert.False(parsed.RootElement.TryGetProperty("provenance", out _));
    }

    [Fact]
    public void Provenance_reads_back_and_leaves_the_facts_as_they_were()
    {
        string json = ChangelogJsonSerializer.Serialize(Facts, null, new RunProvenance("0.1.0", "anthropic", "m", null));

        ChangelogDocument document = ChangelogJsonSerializer.Deserialize(json);

        Assert.Equal(new ChangelogProvenance("0.1.0", "anthropic", "m", null), document.Provenance);
        Assert.Equal(Facts, ChangelogJsonSerializer.ToFactBase(document));
    }

    [Fact]
    public void The_prompt_hash_is_a_sha256_of_the_instructions_and_stable_across_calls()
    {
        Assert.Matches("^sha256:[0-9a-f]{64}$", ChangelogPromptBuilder.PromptHash);
        Assert.Equal(ChangelogPromptBuilder.PromptHash, ChangelogPromptBuilder.PromptHash);
    }
}
