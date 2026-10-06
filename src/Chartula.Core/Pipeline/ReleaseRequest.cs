using Chartula.Core.Llm;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Pipeline;

/// <summary>How a pipeline run treats its outputs.</summary>
public enum PipelineMode
{
    /// <summary>Establish the facts and stop before the model: no call, nothing written or published.</summary>
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
    /// previous tag, or at the first commit when there is none.
    /// </summary>
    public string? Since { get; init; }

    /// <summary>
    /// Whether the operator confirmed a large range up front (<c>--yes</c>).
    /// A run without a terminal has no one to ask, so this is how it reads a range
    /// <see cref="LargeRangeRule"/> applies to.
    /// </summary>
    public bool RangeConfirmed { get; init; }

    /// <summary>
    /// Whether the notes of a release that is already published may be replaced
    /// (<c>--replace-published</c>). They are public and may hold what someone wrote by
    /// hand, so a run replaces them only when asked to (#334).
    /// </summary>
    public bool ReplacePublished { get; init; }
}
