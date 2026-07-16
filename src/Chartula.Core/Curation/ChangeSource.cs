namespace Chartula.Core.Curation;

/// <summary>
/// Where a resolved change's information came from. When PR discipline is
/// imperfect, Chartula degrades from the richer pull-request source to raw
/// commit data rather than failing.
/// </summary>
public enum ChangeSource
{
    /// <summary>The change is backed by a merged pull request.</summary>
    PullRequest,

    /// <summary>The change is backed by commit data (no usable pull request).</summary>
    Commit,
}
