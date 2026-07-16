namespace Chartula.Cli.Configuration;

/// <summary>
/// The <c>Chartula:Faithfulness</c> configuration section (read from
/// <c>chartula.yaml</c> once that source is wired). The thorough second-pass check
/// is on by default; set <see cref="Thorough"/> to <c>false</c> to disable it.
/// </summary>
public sealed class FaithfulnessOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Chartula:Faithfulness";

    /// <summary>Whether the thorough (second-pass LLM) check runs. Defaults to on.</summary>
    public bool Thorough { get; init; } = true;
}
