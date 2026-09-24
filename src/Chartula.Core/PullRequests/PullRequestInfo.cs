namespace Chartula.Core.PullRequests;

/// <summary>
/// A merged pull request behind a release, reduced to the fields curation and the
/// fact base need. These are established facts read from the platform, never
/// LLM-generated.
/// </summary>
/// <param name="Number">The pull request number.</param>
/// <param name="Title">The pull request title.</param>
/// <param name="Description">The pull request body, or <c>null</c> if empty.</param>
/// <param name="Labels">The label names applied to the pull request.</param>
/// <param name="Url">The web link to the pull request.</param>
public sealed record PullRequestInfo(
    int Number,
    string Title,
    string? Description,
    IReadOnlyList<string> Labels,
    string Url)
{
    /// <summary>
    /// The commits in the release range that belong to this pull request, as full hashes.
    /// RevertPairing uses them to match a revert that names commits to the pull
    /// requests it takes back. Empty when the source does not provide them.
    /// </summary>
    public IReadOnlyList<string> CommitShas { get; init; } = [];
}
