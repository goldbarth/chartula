using System.Text.Json.Serialization;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;

namespace Chartula.Core.Serialization;

/// <summary>
/// One fact as the run record keeps it: every field of a <see cref="ChangeFact"/>,
/// the pull request description included.
/// <c>changelog.json</c> is the release payload and leaves the description out, since a
/// description is what its author wrote for reviewers (#260). The run record stays on
/// the machine, so it keeps the complete fact base a run rendered from.
/// </summary>
public sealed record FactEntry(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("userVisible")] bool UserVisible,
    [property: JsonPropertyName("breaking")] bool Breaking,
    [property: JsonPropertyName("linkedIssues")] IReadOnlyList<int> LinkedIssues,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("description")] string? Description)
{
    public static FactEntry From(ChangeFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return new FactEntry(
            fact.Title,
            fact.Number,
            fact.Url,
            fact.Category.ToString(),
            fact.IsUserVisible,
            fact.IsBreaking,
            fact.LinkedIssues,
            fact.Labels,
            fact.Description);
    }

    public ChangeFact ToFact()
        => new(
            Title,
            Number,
            Url,
            ParseCategory(Category),
            UserVisible,
            Breaking,
            LinkedIssues ?? [],
            Labels ?? [],
            Description);

    public bool Equals(FactEntry? other)
        => other is not null
           && Title == other.Title
           && Number == other.Number
           && Url == other.Url
           && Category == other.Category
           && UserVisible == other.UserVisible
           && Breaking == other.Breaking
           && LinkedIssues.SequenceEqual(other.LinkedIssues)
           && Labels.SequenceEqual(other.Labels)
           && Description == other.Description;

    public override int GetHashCode() => HashCode.Combine(Title, Number, Category, Description);

    private static ChangeCategory ParseCategory(string category)
        => Enum.TryParse(category, ignoreCase: true, out ChangeCategory parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Unknown category '{category}'. Valid categories: "
                + $"{string.Join(", ", Enum.GetNames<ChangeCategory>())}.");
}
