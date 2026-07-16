using Chartula.Core.PullRequests;

namespace Chartula.Core.Pipeline;

/// <summary>How a pipeline run treats its outputs.</summary>
public enum PipelineMode
{
    /// <summary>Produce everything but write and publish nothing (dry run).</summary>
    Preview,

    /// <summary>Produce everything and write the outputs.</summary>
    Generate,
}

/// <summary>What to generate a changelog for.</summary>
/// <param name="Tag">The release tag.</param>
/// <param name="Repository">The repository the release belongs to.</param>
public sealed record ReleaseRequest(string Tag, RepositoryCoordinates Repository);
