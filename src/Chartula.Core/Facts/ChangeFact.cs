using Chartula.Core.Categorization;

namespace Chartula.Core.Facts;

/// <summary>
/// The "index card" for one change: the established facts behind it in one object.
/// The LLM only rephrases from this record, so it is the single source of truth.
/// Every field is derived deterministically from the pull request or commit.
/// Nothing here is LLM-generated.
/// </summary>
/// <param name="Title">The change title, verbatim from the source.</param>
/// <param name="Number">The pull request number, or <c>null</c> for commit-based changes.</param>
/// <param name="Url">The pull request link, or <c>null</c> for commit-based changes.</param>
/// <param name="Category">The category decided by deterministic categorization.</param>
/// <param name="IsUserVisible">Whether the change is visible to end users.</param>
/// <param name="IsBreaking">Whether the change is a breaking change.</param>
/// <param name="LinkedIssues">Numbers of issues linked to the change.</param>
/// <param name="Labels">
/// The labels on the pull request, verbatim and unfiltered.
/// Empty when the source carries none, as with a commit-based change.
/// Which labels a rendering shows is the rendering's decision, not a fact.
/// </param>
/// <param name="Description">
/// The source description, when depth includes it; otherwise <c>null</c>.
/// </param>
public sealed record ChangeFact(
    string Title,
    int? Number,
    string? Url,
    ChangeCategory Category,
    bool IsUserVisible,
    bool IsBreaking,
    IReadOnlyList<int> LinkedIssues,
    IReadOnlyList<string> Labels,
    string? Description)
{
    // Compare LinkedIssues and Labels by content, because a fact is a value.
    // The default record equality compares lists by reference, so two facts with the
    // same linked issues in different list instances would be unequal.
    public bool Equals(ChangeFact? other)
        => other is not null
           && Title == other.Title
           && Number == other.Number
           && Url == other.Url
           && Category == other.Category
           && IsUserVisible == other.IsUserVisible
           && IsBreaking == other.IsBreaking
           && Description == other.Description
           && LinkedIssues.SequenceEqual(other.LinkedIssues)
           && Labels.SequenceEqual(other.Labels);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Title);
        hash.Add(Number);
        hash.Add(Url);
        hash.Add(Category);
        hash.Add(IsUserVisible);
        hash.Add(IsBreaking);
        hash.Add(Description);
        foreach (int issue in LinkedIssues)
        {
            hash.Add(issue);
        }

        foreach (string label in Labels)
        {
            hash.Add(label);
        }

        return hash.ToHashCode();
    }
}
