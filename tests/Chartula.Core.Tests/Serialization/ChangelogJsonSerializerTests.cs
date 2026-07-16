using System.Text.Json;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Serialization;

public sealed class ChangelogJsonSerializerTests
{
    [Fact]
    public void Stores_the_customer_and_product_audience_texts()
    {
        Dictionary<Audience, string> renderings = new()
        {
            [Audience.Technical] = "Technical text.",
            [Audience.Customer] = "Dark mode is here.",
            [Audience.Product] = "Grouped by theme.",
        };

        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Sample(), renderings));
        JsonElement stored = parsed.RootElement.GetProperty("renderings");

        Assert.Equal("Dark mode is here.", stored.GetProperty("customer").GetString());
        Assert.Equal("Grouped by theme.", stored.GetProperty("product").GetString());
        Assert.Equal("Technical text.", stored.GetProperty("technical").GetString());
    }

    [Fact]
    public void Writes_an_empty_renderings_object_when_none_are_provided()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Sample()));
        JsonElement stored = parsed.RootElement.GetProperty("renderings");

        Assert.Equal(JsonValueKind.Object, stored.ValueKind);
        Assert.Empty(stored.EnumerateObject());
    }

    [Fact]
    public void Round_trips_the_stored_renderings()
    {
        Dictionary<Audience, string> renderings = new() { [Audience.Customer] = "Dark mode is here." };

        ChangelogDocument document = ChangelogJsonSerializer.Deserialize(
            ChangelogJsonSerializer.Serialize(Sample(), renderings));

        Assert.Equal("Dark mode is here.", document.Renderings["customer"]);
    }

    private static FactBase Sample() => new("v1.2.0", [
        new ChangeFact("feat: add dark mode", 42, "https://example/pull/42",
            ChangeCategory.Feature, IsUserVisible: true, IsBreaking: false, [12], "Adds a dark theme."),
        new ChangeFact("fix: crash on start", null, null,
            ChangeCategory.Fix, IsUserVisible: true, IsBreaking: false, [], null),
    ]);

    [Fact]
    public void Produces_valid_parseable_json()
    {
        string json = ChangelogJsonSerializer.Serialize(Sample());

        // Parses without throwing - it is valid JSON.
        using JsonDocument parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public void Writes_the_documented_top_level_shape()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Sample()));
        JsonElement root = parsed.RootElement;

        Assert.Equal(ChangelogJsonSerializer.SchemaVersion, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("v1.2.0", root.GetProperty("tag").GetString());
        Assert.Equal(2, root.GetProperty("changes").GetArrayLength());
    }

    [Fact]
    public void Writes_each_documented_field_with_the_category_as_its_name()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Sample()));
        JsonElement change = parsed.RootElement.GetProperty("changes")[0];

        Assert.Equal("feat: add dark mode", change.GetProperty("title").GetString());
        Assert.Equal(42, change.GetProperty("number").GetInt32());
        Assert.Equal("https://example/pull/42", change.GetProperty("url").GetString());
        Assert.Equal("Feature", change.GetProperty("category").GetString());
        Assert.True(change.GetProperty("userVisible").GetBoolean());
        Assert.False(change.GetProperty("breaking").GetBoolean());
        Assert.Equal([12], change.GetProperty("linkedIssues").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal("Adds a dark theme.", change.GetProperty("description").GetString());
    }

    [Fact]
    public void Writes_nullable_fields_as_json_null_for_commit_based_changes()
    {
        using JsonDocument parsed = JsonDocument.Parse(ChangelogJsonSerializer.Serialize(Sample()));
        JsonElement change = parsed.RootElement.GetProperty("changes")[1];

        Assert.Equal(JsonValueKind.Null, change.GetProperty("number").ValueKind);
        Assert.Equal(JsonValueKind.Null, change.GetProperty("url").ValueKind);
        Assert.Equal(JsonValueKind.Null, change.GetProperty("description").ValueKind);
        Assert.Equal(0, change.GetProperty("linkedIssues").GetArrayLength());
    }

    [Fact]
    public void Round_trips_through_the_documented_document_type()
    {
        ChangelogDocument document = ChangelogJsonSerializer.Deserialize(
            ChangelogJsonSerializer.Serialize(Sample()));

        Assert.Equal("v1.2.0", document.Tag);
        Assert.Equal("Feature", document.Changes[0].Category);
        Assert.Equal([12], document.Changes[0].LinkedIssues);
    }
}
