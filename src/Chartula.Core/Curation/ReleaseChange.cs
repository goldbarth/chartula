namespace Chartula.Core.Curation;

/// <summary>
/// One change in a release, resolved from the best available source (a merged
/// pull request when possible, otherwise commit data). This is the degraded-but-
/// useful input the curation and fact-base steps build on.
/// </summary>
/// <param name="Title">The best available human-readable title for the change.</param>
/// <param name="Description">The change body, when a source provides one.</param>
/// <param name="Number">The pull request number, or <c>null</c> for commit-based changes.</param>
/// <param name="Url">The pull request link, or <c>null</c> for commit-based changes.</param>
/// <param name="Labels">Labels from the pull request; empty for commit-based changes.</param>
/// <param name="Source">Where the information came from.</param>
/// <param name="CommitSha">The backing commit hash for commit-based changes, else <c>null</c>.</param>
public sealed record ReleaseChange(
    string Title,
    string? Description,
    int? Number,
    string? Url,
    IReadOnlyList<string> Labels,
    ChangeSource Source,
    string? CommitSha);
