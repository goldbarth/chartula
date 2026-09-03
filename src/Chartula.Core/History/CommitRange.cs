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
/// <param name="TaggedAt">
/// The date the release tag was created, or <c>null</c> when it could not be
/// read. It is the source for the published page's <c>publishedAt</c>, and a
/// field with no source is omitted rather than emitted empty - which is why this
/// is nullable rather than defaulted to today.
/// </param>
public sealed record CommitRange(
    string ToTag,
    string? FromTag,
    IReadOnlyList<CommitInfo> Commits,
    DateOnly? TaggedAt = null)
{
    /// <summary>True when there is no previous tag and the range is all history.</summary>
    public bool IsFirstRelease => FromTag is null;
}
