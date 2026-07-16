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
    string? Description);
