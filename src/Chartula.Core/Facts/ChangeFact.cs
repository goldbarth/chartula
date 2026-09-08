using Chartula.Core.Categorization;

namespace Chartula.Core.Facts;

/// <summary>
/// The "index card" for one change: a single structured object holding the
/// established facts behind it. This is the single source of truth the LLM may
/// only rephrase from. Every field is a fact derived deterministically from the
/// pull request or commit; nothing here is LLM-generated.
/// </summary>
/// <param name="Title">The change title, verbatim from the source.</param>
/// <param name="Number">The pull request number, or <c>null</c> for commit-based changes.</param>
/// <param name="Url">The pull request link, or <c>null</c> for commit-based changes.</param>
/// <param name="Category">The category decided by deterministic categorization.</param>
/// <param name="IsUserVisible">Whether the change is visible to end users.</param>
/// <param name="IsBreaking">Whether the change is a breaking change.</param>
/// <param name="LinkedIssues">Numbers of issues linked to the change.</param>
/// <param name="Labels">
/// The labels on the pull request, verbatim and unfiltered; empty when the source
/// carries none, as a commit-based change does. Which of them a rendering shows is
/// a rendering's decision, not a fact.
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
    // A record compares its properties with the default equality comparer, which for
    // a list means reference identity. Two facts carrying the same linked issues in
    // different list instances would then be unequal, which is not what a fact is: it
    // is a value. Comparing the issues and labels by content is what makes it one.
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
