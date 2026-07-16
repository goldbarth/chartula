using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;

namespace Chartula.Core.Pipeline;

/// <summary>
/// Default <see cref="IReleasePipeline"/>. It reads the release history and pull
/// requests, builds the fact base, renders every audience, runs the rule-based and
/// thorough faithfulness checks and review, and writes the outputs. The only
/// difference between preview and generate is the final write step: preview writes
/// and publishes nothing. The technical rendering feeds CHANGELOG.md and the
/// release notes; every audience text is stored in changelog.json.
/// </summary>
public sealed class ReleasePipeline(
    IReleaseCommitReader commitReader,
    IReleasePullRequestReader pullRequestReader,
    IFactBaseBuilder factBaseBuilder,
    IReleaseRenderer renderer,
    IRuleBasedFaithfulnessChecker ruleBasedChecker,
    IThoroughFaithfulnessChecker thoroughChecker,
    IReviewCoordinator reviewCoordinator,
    IChangelogJsonWriter jsonWriter,
    IChangelogMarkdownWriter markdownWriter,
    IReleaseNotesWriter releaseNotesWriter) : IReleasePipeline
{
    public async Task<ReleaseOutcome> RunAsync(
        ReleaseRequest request,
        PipelineMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CommitRange range = await commitReader.ReadReleaseCommitsAsync(request.Tag, cancellationToken);
        IReadOnlyList<PullRequestInfo> pullRequests =
            await pullRequestReader.GetMergedPullRequestsAsync(request.Repository, range, cancellationToken);
        FactBase factBase = factBaseBuilder.Build(range, pullRequests);

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> rendered =
            await renderer.RenderAllAsync(factBase, cancellationToken);

        List<AudienceOutcome> outcomes = [];
        Dictionary<Audience, string> finalTexts = [];
        foreach ((Audience audience, ChangelogGenerationResult result) in rendered.OrderBy(entry => entry.Key))
        {
            if (!result.IsSuccess)
            {
                outcomes.Add(new AudienceOutcome(audience, Success: false, Text: null, Flags: [], result.Error));
                continue;
            }

            string text = result.Text ?? string.Empty;
            IReadOnlyList<string> flags = await CollectFlagsAsync(text, factBase, cancellationToken);

            ReviewDecision decision =
                await reviewCoordinator.ReviewAsync(new ReviewItem(audience, text, flags), cancellationToken);

            finalTexts[audience] = decision.Text;
            outcomes.Add(new AudienceOutcome(audience, Success: true, decision.Text, flags, Error: null));
        }

        IReadOnlyList<string> written = mode == PipelineMode.Generate
            ? await WriteOutputsAsync(request, factBase, finalTexts, cancellationToken)
            : [];

        return new ReleaseOutcome(request.Tag, mode, outcomes, written);
    }

    private async Task<IReadOnlyList<string>> CollectFlagsAsync(
        string text, FactBase factBase, CancellationToken cancellationToken)
    {
        List<string> flags = [.. ruleBasedChecker.Check(text, factBase).UnsupportedClaims];
        FaithfulnessReport thorough = await thoroughChecker.CheckAsync(text, factBase, cancellationToken);
        flags.AddRange(thorough.UnsupportedClaims);
        return flags;
    }

    private async Task<IReadOnlyList<string>> WriteOutputsAsync(
        ReleaseRequest request,
        FactBase factBase,
        IReadOnlyDictionary<Audience, string> finalTexts,
        CancellationToken cancellationToken)
    {
        List<string> written = [];

        written.Add(await jsonWriter.WriteAsync(factBase, finalTexts, cancellationToken));

        if (finalTexts.TryGetValue(Audience.Technical, out string? technical))
        {
            written.Add(await markdownWriter.WriteAsync(request.Tag, technical, cancellationToken));
            written.Add(await releaseNotesWriter.WriteAsync(
                request.Repository, request.Tag, technical, cancellationToken));
        }

        return written;
    }
}
