namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:FactBase</c> configuration section (read from <c>chartula.yaml</c>
/// once that source is wired). Leaving <see cref="Depth"/> unset keeps the default
/// (title and description).
/// </summary>
public sealed class FactBaseOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:FactBase";

    /// <summary>
    /// How much source material feeds the fact base: title-only,
    /// title-and-description (default), or title-description-and-issues.
    /// </summary>
    public string? Depth { get; init; }
}
