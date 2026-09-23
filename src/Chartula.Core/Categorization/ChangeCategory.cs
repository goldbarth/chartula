namespace Chartula.Core.Categorization;

/// <summary>
/// The kind of change, decided deterministically from commit and PR conventions.
/// It is decided before any LLM runs, so the model cannot invent the kind of change.
/// </summary>
public enum ChangeCategory
{
    /// <summary>A new feature (<c>feat</c>).</summary>
    Feature,

    /// <summary>A bug fix (<c>fix</c>).</summary>
    Fix,

    /// <summary>A performance improvement (<c>perf</c>).</summary>
    Performance,

    /// <summary>Documentation only (<c>docs</c>).</summary>
    Documentation,

    /// <summary>A code refactor with no behaviour change (<c>refactor</c>).</summary>
    Refactor,

    /// <summary>Internal work: build, CI, chores, tests, style, reverts.</summary>
    Internal,

    /// <summary>The sane default when no known convention is recognized.</summary>
    Other,
}
