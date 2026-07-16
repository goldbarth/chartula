namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:Categories</c> configuration section (read from
/// <c>chartula.yaml</c>). Controls the order categories appear in, their display
/// names, and whether breaking changes are shown prominently.
/// </summary>
public sealed class CategoryOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Categories";

    /// <summary>Category names in display order. <c>null</c> keeps the default order.</summary>
    public List<string>? Order { get; init; }

    /// <summary>Map of category name to display name.</summary>
    public Dictionary<string, string> Names { get; init; } = [];

    /// <summary>Whether breaking changes float to the top. Defaults to on.</summary>
    public bool BreakingProminent { get; init; } = true;
}
