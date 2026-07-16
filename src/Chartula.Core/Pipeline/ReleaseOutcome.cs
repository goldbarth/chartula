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
    IReadOnlyList<string> Flags,
    string? Error);

/// <summary>The result of a pipeline run.</summary>
/// <param name="Tag">The release tag.</param>
/// <param name="Mode">Whether the run previewed or generated.</param>
/// <param name="Renderings">One outcome per audience.</param>
/// <param name="WrittenOutputs">
/// Paths and links written this run. Empty in preview mode - nothing is written.
/// </param>
/// <param name="Metrics">What the run did and what it cost in tokens.</param>
public sealed record ReleaseOutcome(
    string Tag,
    PipelineMode Mode,
    IReadOnlyList<AudienceOutcome> Renderings,
    IReadOnlyList<string> WrittenOutputs)
{
    public RunReport Metrics { get; init; } = RunReport.Empty;

    public ReleaseOutcome(
        string tag,
        PipelineMode mode,
        IReadOnlyList<AudienceOutcome> renderings,
        IReadOnlyList<string> writtenOutputs,
        RunReport metrics)
        : this(tag, mode, renderings, writtenOutputs)
        => Metrics = metrics;
}
