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
    /// The audiences to render, or <c>null</c> for all of them, which is what a release needs.
    /// Each audience costs its own rephrasing call and faithfulness check, while the
    /// fact base is shared. So requesting fewer audiences costs less.
    /// This exists to measure one audience's wording.
    /// <para>
    /// An output whose audience was not rendered is not written. This needs no special
    /// case: the pipeline writes each output only when its rendering is present.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Audience>? Audiences { get; init; }

    /// <summary>
    /// The tag or commit the release starts after, or <c>null</c> to start after the
    /// previous tag.
    /// Where a release starts is a fact decision, so the operator names it. On a first
    /// tag, this is the only possible start.
    /// </summary>
    public string? Since { get; init; }

    /// <summary>
    /// Whether a range that spans all history may be rendered.
    /// A first tag without <see cref="Since"/> has such a range. Rendered as is, it
    /// reads as a development log: intermediate states next to the changes that
    /// replaced them.
    /// So it is refused unless requested, for a project whose whole history is the release.
    /// </summary>
    public bool WholeHistory { get; init; }
}
