namespace Chartula.Core.Pipeline;

/// <summary>
/// Runs the full release pipeline: read history and pull requests, build the fact
/// base, render every audience, run the faithfulness checks and review.
/// The modes differ only in what is written:
/// <list type="bullet">
/// <item><see cref="PipelineMode.Preview"/> writes and publishes nothing.</item>
/// <item><see cref="PipelineMode.Generate"/> writes the outputs and publishes the release notes.</item>
/// <item><see cref="PipelineMode.GenerateWithoutPublishing"/> writes the local files and
/// leaves the release notes alone.</item>
/// </list>
/// </summary>
public interface IReleasePipeline
{
    Task<ReleaseOutcome> RunAsync(
        ReleaseRequest request,
        PipelineMode mode,
        CancellationToken cancellationToken = default);
}
