namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:Labels</c> configuration section (read from <c>chartula.yaml</c>
/// once that source is wired). Config-shaped: category values are names that map
/// to <c>ChangeCategory</c> when the domain rules are built.
/// </summary>
public sealed class LabelOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Labels";

    /// <summary>Labels that exclude a change from the changelog.</summary>
    public List<string> Exclude { get; init; } = [];

    /// <summary>Map of label name to category name (forces that change's category).</summary>
    public Dictionary<string, string> Category { get; init; } = [];

    /// <summary>When true, only changes carrying at least one label are included.</summary>
    public bool OnlyIncludeLabeled { get; init; }
}
