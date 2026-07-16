namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:Filter</c> configuration section (read from <c>chartula.yaml</c>
/// once that source is wired). Leaving <see cref="ExcludeCategories"/> unset keeps
/// the default (exclude internal work); an explicit list replaces it.
/// </summary>
public sealed class FilterOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Filter";

    /// <summary>
    /// Category names to exclude. <c>null</c> keeps the default; an explicit
    /// (possibly empty) list overrides it.
    /// </summary>
    public List<string>? ExcludeCategories { get; init; }
}
