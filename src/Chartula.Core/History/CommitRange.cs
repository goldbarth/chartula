namespace Chartula.Core.History;

/// <summary>
/// The commits belonging to a release: everything after the previous tag up to
/// and including the release tag. When there is no previous tag, this is the
/// first release and spans all history up to the tag.
/// </summary>
/// <param name="ToTag">The release tag the commits belong to.</param>
/// <param name="FromTag">
/// The previous tag the range starts after, or <c>null</c> for the first release.
/// </param>
/// <param name="Commits">The commits in the range.</param>
public sealed record CommitRange(
    string ToTag,
    string? FromTag,
    IReadOnlyList<CommitInfo> Commits)
{
    /// <summary>True when there is no previous tag and the range is all history.</summary>
    public bool IsFirstRelease => FromTag is null;
}
