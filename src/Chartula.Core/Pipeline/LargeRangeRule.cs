using Chartula.Core.History;

namespace Chartula.Core.Pipeline;

/// <summary>
/// Which ranges are confirmed before anything is read for them.
/// A range costs one GitHub request per commit and sends every change in it to the model,
/// so a run over far more than was meant costs before anyone sees its output.
/// A first tag is always confirmed, whatever its size: its range is every commit up to it,
/// which is the release for a new project and a development log for one that tags late.
/// </summary>
/// <param name="CommitThreshold">A range with more commits than this is confirmed.</param>
public sealed record LargeRangeRule(int CommitThreshold)
{
    /// <summary>
    /// The threshold when none is configured.
    /// Well above a release of a few dozen pull requests, even one merged with merge commits,
    /// and well below GitHub's hourly limit of 5,000 authenticated requests.
    /// </summary>
    public const int DefaultCommitThreshold = 200;

    public static LargeRangeRule Default { get; } = new(DefaultCommitThreshold);

    /// <summary>Whether <paramref name="range"/> is confirmed before it is read.</summary>
    public bool Applies(CommitRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        return range.StartsAtFirstCommit || range.Commits.Count > CommitThreshold;
    }
}
