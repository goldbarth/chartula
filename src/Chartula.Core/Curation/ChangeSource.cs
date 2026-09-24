namespace Chartula.Core.Curation;

/// <summary>
/// Where a resolved change's information came from.
/// Without usable pull requests, Chartula falls back to raw commit data instead of failing.
/// </summary>
public enum ChangeSource
{
    /// <summary>The change is backed by a merged pull request.</summary>
    PullRequest,

    /// <summary>The change is backed by commit data (no usable pull request).</summary>
    Commit,
}
