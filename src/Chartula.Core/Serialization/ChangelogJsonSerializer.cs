using System.Text.Json;
using System.Text.Json.Serialization;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Serialization;

/// <summary>
/// Serializes a <see cref="FactBase"/> to the stable <c>changelog.json</c> format
/// (and back). Pure and deterministic; uses source-generated metadata so it stays
/// AOT- and trim-safe. The category is written as its name for a stable, readable
/// record. The rendered audience texts are stored alongside the facts, so the
/// customer and product versions are backed up without extra files in the repo.
/// </summary>
public static class ChangelogJsonSerializer
{
    /// <summary>The current on-disk schema version.</summary>
    public const int SchemaVersion = 1;

    // Fixed order, so the stored renderings object is deterministic.
    private static readonly Audience[] RenderingOrder =
        [Audience.Technical, Audience.Customer, Audience.Product];

    public static string Serialize(
        FactBase factBase,
        IReadOnlyDictionary<Audience, string>? renderings = null)
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
                change.Description)).ToArray(),
            BuildRenderings(renderings));

        return JsonSerializer.Serialize(document, ChangelogJsonContext.Default.ChangelogDocument);
    }

    private static Dictionary<string, string> BuildRenderings(IReadOnlyDictionary<Audience, string>? renderings)
    {
        Dictionary<string, string> map = [];
        if (renderings is null)
        {
            return map;
        }

        foreach (Audience audience in RenderingOrder)
        {
            if (renderings.TryGetValue(audience, out string? text))
            {
                map[audience.ToString().ToLowerInvariant()] = text;
            }
        }

        return map;
    }

    /// <summary>Reads a document back, for tests and downstream outputs.</summary>
    public static ChangelogDocument Deserialize(string json)
        => JsonSerializer.Deserialize(json, ChangelogJsonContext.Default.ChangelogDocument)
           ?? throw new InvalidOperationException("changelog.json deserialized to null.");

    /// <summary>
    /// Reads the facts back out of a stored document. The inverse of
    /// <see cref="Serialize"/>: a written <c>changelog.json</c> round-trips to the fact
    /// base it came from, which is what lets a real release be frozen and replayed.
    /// The renderings are not part of the facts and are dropped.
    /// </summary>
    public static FactBase DeserializeFactBase(string json) => ToFactBase(Deserialize(json));

    /// <summary>The facts held by a document, without its renderings.</summary>
    public static FactBase ToFactBase(ChangelogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.SchemaVersion != SchemaVersion)
        {
            throw new InvalidOperationException(
                $"changelog.json has schema version {document.SchemaVersion}, but this version of "
                + $"Chartula reads version {SchemaVersion}.");
        }

        return new FactBase(
            document.Tag,
            [.. document.Changes.Select(static change => new ChangeFact(
                change.Title,
                change.Number,
                change.Url,
                ParseCategory(change.Category),
                change.UserVisible,
                change.Breaking,
                change.LinkedIssues,
                change.Description))]);
    }

    private static ChangeCategory ParseCategory(string category)
        => Enum.TryParse(category, ignoreCase: true, out ChangeCategory parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Unknown category '{category}' in changelog.json. Valid categories: "
                + $"{string.Join(", ", Enum.GetNames<ChangeCategory>())}.");
}

/// <summary>Source-generated (reflection-free) context for the changelog format.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ChangelogDocument))]
internal sealed partial class ChangelogJsonContext : JsonSerializerContext;
