namespace Chartula.Core.Pipeline;

/// <summary>
/// Runs the full release pipeline: read history and pull requests, build the fact
/// base, render every audience, run the faithfulness checks and review, and - in
/// the generate modes - write the outputs. In preview mode it produces the same
/// result but writes and publishes nothing; without publishing it writes the local
/// files and leaves the release notes alone.
/// </summary>
public interface IReleasePipeline
{
    Task<ReleaseOutcome> RunAsync(
        ReleaseRequest request,
        PipelineMode mode,
        CancellationToken cancellationToken = default);
}
