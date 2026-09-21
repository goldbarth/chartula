namespace Chartula.Core.Pipeline;

/// <summary>
/// A run whose range is all history, which was not asked for. Its own type so the
/// command surface can name its own way out - the domain does not know the flags.
/// </summary>
public sealed class WholeHistoryException(string tag, int commitCount)
    : InvalidOperationException(
        $"{tag} is the first tag, so its range is the whole history ({commitCount} commits). " +
        "Name where the release starts, or render the whole history on purpose.")
{
    /// <summary>The release tag.</summary>
    public string Tag { get; } = tag;

    /// <summary>How many commits the whole history holds.</summary>
    public int CommitCount { get; } = commitCount;
}
