using System.Text.Json;
using System.Text.Json.Serialization;
using Chartula.Core.Facts;

namespace Chartula.Core.Serialization;

/// <summary>
/// Serializes a <see cref="FactBase"/> to the stable <c>changelog.json</c> format
/// (and back). Pure and deterministic; uses source-generated metadata so it stays
/// AOT- and trim-safe. The category is written as its name for a stable, readable
/// record.
/// </summary>
public static class ChangelogJsonSerializer
{
    /// <summary>The current on-disk schema version.</summary>
    public const int SchemaVersion = 1;

    public static string Serialize(FactBase factBase)
    {
        ArgumentNullException.ThrowIfNull(factBase);

        ChangelogDocument document = new(
            SchemaVersion,
            factBase.Tag,
            factBase.Changes.Select(static change => new ChangelogChange(
                change.Title,
                change.Number,
                change.Url,
                change.Category.ToString(),
                change.IsUserVisible,
                change.IsBreaking,
                change.LinkedIssues,
                change.Description)).ToArray());

        return JsonSerializer.Serialize(document, ChangelogJsonContext.Default.ChangelogDocument);
    }

    /// <summary>Reads a document back, for tests and downstream outputs.</summary>
    public static ChangelogDocument Deserialize(string json)
        => JsonSerializer.Deserialize(json, ChangelogJsonContext.Default.ChangelogDocument)
           ?? throw new InvalidOperationException("changelog.json deserialized to null.");
}

/// <summary>Source-generated (reflection-free) context for the changelog format.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ChangelogDocument))]
internal sealed partial class ChangelogJsonContext : JsonSerializerContext;
