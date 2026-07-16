using System.Text.Json.Serialization;

namespace Chartula.Core.Serialization;

/// <summary>
/// The stable on-disk shape of <c>changelog.json</c>. Kept separate from the
/// domain <c>FactBase</c> so the domain can evolve without breaking the file
/// format; <see cref="SchemaVersion"/> is bumped when the format changes in a
/// breaking way. Documented in <c>docs/changelog-json.md</c>.
/// </summary>
public sealed record ChangelogDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("changes")] IReadOnlyList<ChangelogChange> Changes,
    [property: JsonPropertyName("renderings")] IReadOnlyDictionary<string, string> Renderings);

/// <summary>One change entry in <see cref="ChangelogDocument"/>.</summary>
public sealed record ChangelogChange(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("userVisible")] bool UserVisible,
    [property: JsonPropertyName("breaking")] bool Breaking,
    [property: JsonPropertyName("linkedIssues")] IReadOnlyList<int> LinkedIssues,
    [property: JsonPropertyName("description")] string? Description);
