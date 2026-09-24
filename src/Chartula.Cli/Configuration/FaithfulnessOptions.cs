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

    /// <summary>
    /// The model for the thorough check, at the same provider and endpoint as the rendering.
    /// Unset uses <c>llm.model</c>.
    /// Rendering writes prose, checking compares claims with facts. These are different
    /// jobs, so a cheaper model can do one and a stronger model the other.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// How much the thorough check's model reasons, in the values of
    /// <c>llm.thinking</c>. Unset uses <c>llm.thinking</c>.
    /// </summary>
    public string? Thinking { get; init; }
}
