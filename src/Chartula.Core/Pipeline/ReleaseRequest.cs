using Chartula.Core.Llm;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Pipeline;

/// <summary>How a pipeline run treats its outputs.</summary>
public enum PipelineMode
{
    /// <summary>Produce everything but write and publish nothing (dry run).</summary>
    Preview,

    /// <summary>Produce everything and write the outputs.</summary>
    Generate,

    /// <summary>
    /// Produce everything and write the local files, but publish nothing. A record
    /// of a run is not the same act as announcing a release, so the two are
    /// separable: this writes changelog.json and CHANGELOG.md and leaves the
    /// platform's release notes untouched.
    /// </summary>
    GenerateWithoutPublishing,
}

/// <summary>What to generate a changelog for.</summary>
/// <param name="Tag">The release tag.</param>
/// <param name="Repository">The repository the release belongs to.</param>
public sealed record ReleaseRequest(string Tag, RepositoryCoordinates Repository)
{
    /// <summary>
    /// The audiences to render, or <c>null</c> for all of them, which is what a
    /// release wants. A run that asks for fewer pays for fewer: each audience is
    /// its own rephrasing call and its own faithfulness check, while the fact base
    /// behind them is the same. Measuring one audience's wording is the case this
    /// exists for.
    /// <para>
    /// An output whose audience was not rendered is not written. That is not a
    /// special case here: the pipeline already writes each output only when the
    /// rendering it is made of is present.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Audience>? Audiences { get; init; }

    /// <summary>
    /// The tag or commit the release starts after, or <c>null</c> to start after
    /// the previous tag. Where a release starts is a fact decision, so it is the
    /// operator's to name - on a first tag there is nothing else to start from.
    /// </summary>
    public string? Since { get; init; }

    /// <summary>
    /// Whether a range that spans all history may be rendered. A first tag with no
    /// <see cref="Since"/> has such a range, and rendered as it is, it reads as a
    /// development log: intermediate states next to the changes that replaced
    /// them. So it is refused unless asked for, for a project whose history is the
    /// release.
    /// </summary>
    public bool WholeHistory { get; init; }
}
