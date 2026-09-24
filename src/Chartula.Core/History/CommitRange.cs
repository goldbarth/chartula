namespace Chartula.Core.History;

/// <summary>
/// The commits belonging to a release: everything after its start, up to and
/// including the release tag.
/// The start is the previous tag or a ref the operator named.
/// Without either, the range is all history up to the tag.
/// </summary>
/// <param name="ToTag">The release tag the commits belong to.</param>
/// <param name="From">
/// The tag or commit the range starts after.
/// <c>null</c> when the range spans all history: a first tag with no start named.
/// </param>
/// <param name="Commits">The commits in the range.</param>
/// <param name="TaggedAt">
/// The date the release tag was created, or <c>null</c> when it could not be read.
/// It is the source for the published page's <c>publishedAt</c> and for the date in
/// the <c>CHANGELOG.md</c> release heading.
/// It is nullable instead of defaulting to today, because a field with no source is
/// omitted, not emitted empty.
/// </param>
public sealed record CommitRange(
    string ToTag,
    string? From,
    IReadOnlyList<CommitInfo> Commits,
    DateOnly? TaggedAt = null)
{
    /// <summary>True when nothing bounds the range and it is all history.</summary>
    public bool IsWholeHistory => From is null;
}
