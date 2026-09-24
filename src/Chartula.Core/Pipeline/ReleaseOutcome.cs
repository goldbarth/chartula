using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Core.Pipeline;

/// <summary>The result of rendering one audience in a pipeline run.</summary>
/// <param name="Audience">The audience.</param>
/// <param name="Success">Whether the rendering succeeded.</param>
/// <param name="Text">The final (post-review) text, or <c>null</c> on failure.</param>
/// <param name="Flags">Faithfulness findings surfaced for this rendering.</param>
/// <param name="Error">A clear error message, or <c>null</c> on success.</param>
public sealed record AudienceOutcome(
    Audience Audience,
    bool Success,
    string? Text,
    IReadOnlyList<FaithfulnessFlag> Flags,
    string? Error)
{
    /// <summary>
    /// The one-sentence summary of the release written together with the text, or
    /// <c>null</c> when the audience has none.
    /// A preview that shows the text must show this summary too. Otherwise the preview
    /// could not vouch for the first line of the page.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>The result of a pipeline run.</summary>
/// <param name="Tag">The release tag.</param>
/// <param name="Mode">Whether the run previewed or generated.</param>
/// <param name="Renderings">One outcome per audience.</param>
/// <param name="WrittenOutputs">
/// Paths and links written in this run. Empty in preview mode, which writes nothing.
/// </param>
public sealed record ReleaseOutcome(
    string Tag,
    PipelineMode Mode,
    IReadOnlyList<AudienceOutcome> Renderings,
    IReadOnlyList<string> WrittenOutputs)
{
    /// <summary>What the run did and what it cost in tokens.</summary>
    public RunReport Metrics { get; init; } = RunReport.Empty;

    /// <summary>
    /// Outputs this run deliberately did not produce, listed so a skipped publication
    /// is visible instead of silent. Empty unless a mode leaves something out.
    /// </summary>
    public IReadOnlyList<string> SkippedOutputs { get; init; } = [];

    /// <summary>
    /// Why publishing the release notes failed, or <c>null</c> when it succeeded.
    /// Publishing is the last write. By then the files are written and the model calls
    /// are paid for, so failing the whole run would hide both behind one error.
    /// </summary>
    public string? PublishFailure { get; init; }

    /// <summary>
    /// Where the run record was written. <c>null</c> when none was written: a preview
    /// writes nothing, and a pipeline without a record writer keeps no record.
    /// </summary>
    public string? RunRecord { get; init; }

    public ReleaseOutcome(
        string tag,
        PipelineMode mode,
        IReadOnlyList<AudienceOutcome> renderings,
        IReadOnlyList<string> writtenOutputs,
        RunReport metrics)
        : this(tag, mode, renderings, writtenOutputs)
        => Metrics = metrics;
}
