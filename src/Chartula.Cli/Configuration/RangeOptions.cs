namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:Range</c> configuration section.
/// Leaving <see cref="ConfirmAboveCommits"/> unset keeps the default
/// (<see cref="Core.Pipeline.LargeRangeRule.DefaultCommitThreshold"/>).
/// </summary>
public sealed class RangeOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Range";

    /// <summary>A range with more commits than this is confirmed before it is read.</summary>
    public int? ConfirmAboveCommits { get; init; }
}
