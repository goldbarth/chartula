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
    /// The model the thorough check asks, at the same provider and endpoint as the
    /// rendering. Unset uses <c>llm.model</c>. Rendering and checking are different
    /// jobs - writing prose versus comparing claims with facts - so a cheaper model
    /// can do one and a stronger one the other.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// How much the thorough check's model reasons, in the values of
    /// <c>llm.thinking</c>. Unset uses <c>llm.thinking</c>.
    /// </summary>
    public string? Thinking { get; init; }
}
