namespace Chartula.Core.History;

/// <summary>
/// A single commit belonging to a release, reduced to what the curation and
/// fallback steps need.
/// </summary>
/// <param name="Sha">The full commit hash.</param>
/// <param name="Subject">The commit subject (first line of the message).</param>
public sealed record CommitInfo(string Sha, string Subject)
{
    /// <summary>
    /// Whether the commit has more than one parent. A merge commit brings in other
    /// commits of the range and changes nothing of its own, so it never becomes a change.
    /// </summary>
    public bool IsMerge { get; init; }
}
