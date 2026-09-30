namespace Chartula.Core.History;

/// <summary>
/// The commits belonging to a release: everything after its start, up to and
/// including the release tag.
/// The start is the previous tag or a ref the operator named.
/// Without either, as for a first tag, the range starts at the first commit.
/// </summary>
/// <param name="ToTag">The release tag the commits belong to.</param>
/// <param name="From">
/// The tag or commit the range starts after.
/// <c>null</c> when the range starts at the first commit: a first tag with no start named.
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
    /// <summary>
    /// True when no start bounds the range, so it is every commit up to the tag.
    /// It is not the whole history of the repository: commits after the tag are not in it.
    /// </summary>
    public bool StartsAtFirstCommit => From is null;

    /// <summary>
    /// The full hash of the commit <see cref="From"/> named when the range was read, or
    /// <c>null</c> when the range starts at the first commit or the hash was not read.
    /// A name alone does not keep the range: a tag can be moved, a branch moves by itself.
    /// </summary>
    public string? FromCommit { get; init; }

    /// <summary>
    /// The full hash of the commit <see cref="ToTag"/> named when the range was read, or
    /// <c>null</c> when it was not read.
    /// </summary>
    public string? ToCommit { get; init; }
}
