using System.Text.Json;
using System.Text.Json.Serialization;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Serialization;

/// <summary>
/// Serializes a <see cref="FactBase"/> to the stable <c>changelog.json</c> format and back.
/// Pure and deterministic. It uses source-generated metadata, so it stays AOT- and trim-safe.
/// The category is written as its name, which keeps the record stable and readable.
/// The rendered audience texts are stored next to the facts, so the customer and
/// product versions are kept without extra files in the repository.
/// </summary>
public static class ChangelogJsonSerializer
{
    /// <summary>The current on-disk schema version.</summary>
    public const int SchemaVersion = 1;

    // A fixed order keeps the stored renderings object deterministic.
    private static readonly Audience[] RenderingOrder =
        [Audience.Technical, Audience.Customer, Audience.Product];

    public static string Serialize(
        FactBase factBase,
        IReadOnlyDictionary<Audience, string>? renderings = null,
        RunProvenance? provenance = null)
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
                change.Labels,
                change.Description)).ToArray(),
            BuildRenderings(renderings),
            ToDocument(provenance));

        return JsonSerializer.Serialize(document, ChangelogJsonContext.Default.ChangelogDocument);
    }

    // The run record uses this too, so a run record and the changelog.json it wrote
    // describe their provenance in the same fields.
    internal static ChangelogProvenance? ToDocument(RunProvenance? provenance)
        => provenance is null
            ? null
            : new ChangelogProvenance(
                Present(provenance.ToolVersion),
                Present(provenance.Provider),
                Present(provenance.Model),
                Present(provenance.PromptHash),
                Present(provenance.Thinking),
                provenance.ThoroughCheck,
                Present(provenance.FactBaseDepth),
                Present(provenance.CheckModel),
                Present(provenance.CheckThinking));

    // Treat a blank value like null and leave it out, so the file never contains "".
    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
    /// Reads the facts back out of a stored document. The inverse of <see cref="Serialize"/>.
    /// A written <c>changelog.json</c> reads back to the fact base it came from, so a
    /// real release can be stored and replayed.
    /// The renderings are not facts and are dropped.
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
                // A document written before the labels field existed reads back with
                // an empty list, so the fact base never carries null.
                change.Labels ?? [],
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
