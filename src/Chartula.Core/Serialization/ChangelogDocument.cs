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
    [property: JsonPropertyName("renderings")] IReadOnlyDictionary<string, string> Renderings,
    [property: JsonPropertyName("provenance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ChangelogProvenance? Provenance = null);

/// <summary>
/// How the file was made. Optional and additive under schema version 1: a file
/// written without it, or before it existed, simply has none, and each value the
/// run did not have is left out rather than written empty.
/// </summary>
public sealed record ChangelogProvenance(
    [property: JsonPropertyName("toolVersion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolVersion,
    [property: JsonPropertyName("provider"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Provider,
    [property: JsonPropertyName("model"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Model,
    [property: JsonPropertyName("promptHash"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PromptHash,
    [property: JsonPropertyName("thinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Thinking = null,
    [property: JsonPropertyName("thoroughCheck"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? ThoroughCheck = null,
    [property: JsonPropertyName("factBaseDepth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FactBaseDepth = null,
    [property: JsonPropertyName("checkModel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CheckModel = null,
    [property: JsonPropertyName("checkThinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CheckThinking = null);

/// <summary>One change entry in <see cref="ChangelogDocument"/>.</summary>
public sealed record ChangelogChange(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("userVisible")] bool UserVisible,
    [property: JsonPropertyName("breaking")] bool Breaking,
    [property: JsonPropertyName("linkedIssues")] IReadOnlyList<int> LinkedIssues,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("description")] string? Description);
