using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;

namespace Chartula.Core.Pipeline;

/// <summary>
/// Default <see cref="IReleasePipeline"/>. It reads the release history and pull
/// requests, builds the fact base, renders every audience, runs the rule-based and
/// thorough faithfulness checks and review, and writes the outputs. The modes
/// differ in the final write step alone: preview writes and publishes nothing,
/// generate does both, and generate-without-publishing writes the local files and
/// leaves the release notes alone. The technical rendering feeds CHANGELOG.md and
/// the release notes; every audience text is stored in changelog.json. Along the
/// way it records what each faithfulness check caught, so the run reports its own
/// cost.
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
    IReleaseNotesWriter releaseNotesWriter,
    IRunMetrics? metrics = null) : IReleasePipeline
{
    private readonly IRunMetrics _metrics = metrics ?? NullRunMetrics.Instance;

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

        IReadOnlyList<string> written = [];
        IReadOnlyList<string> skipped = [];
        if (mode != PipelineMode.Preview)
        {
            (written, skipped) = await WriteOutputsAsync(request, factBase, finalTexts, mode, cancellationToken);
        }

        return new ReleaseOutcome(request.Tag, mode, outcomes, written, _metrics.Snapshot())
        {
            SkippedOutputs = skipped,
        };
    }

    private async Task<IReadOnlyList<string>> CollectFlagsAsync(
        string text, FactBase factBase, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ruleBased = ruleBasedChecker.Check(text, factBase).UnsupportedClaims;
        FaithfulnessReport thorough = await thoroughChecker.CheckAsync(text, factBase, cancellationToken);

        // Both findings go in together, so the report can tell what the paid check
        // caught over and above the free one.
        bool thoroughEvaluated = thorough.Status != FaithfulnessCheckStatus.NotEvaluated;
        _metrics.RecordFaithfulnessChecks(ruleBased, thorough.UnsupportedClaims, thoroughEvaluated);

        List<string> flags = [.. ruleBased, .. thorough.UnsupportedClaims];

        // A check that ran and could not be read leaves the text unverified. Without a
        // flag of its own that is indistinguishable from a check that found nothing.
        if (!thoroughEvaluated)
        {
            flags.Add($"The thorough check could not be evaluated: {thorough.Reason}.");
        }

        return flags;
    }

    private async Task<(IReadOnlyList<string> Written, IReadOnlyList<string> Skipped)> WriteOutputsAsync(
        ReleaseRequest request,
        FactBase factBase,
        IReadOnlyDictionary<Audience, string> finalTexts,
        PipelineMode mode,
        CancellationToken cancellationToken)
    {
        List<string> written = [];
        List<string> skipped = [];

        written.Add(await jsonWriter.WriteAsync(factBase, finalTexts, cancellationToken));

        if (finalTexts.TryGetValue(Audience.Technical, out string? technical))
        {
            written.Add(await markdownWriter.WriteAsync(request.Tag, technical, cancellationToken));

            // Writing the record and announcing the release are two acts. Only the
            // second one is skipped here, and it is named rather than passed over in
            // silence, so a run that published nothing says so.
            if (mode == PipelineMode.Generate)
            {
                written.Add(await releaseNotesWriter.WriteAsync(
                    request.Repository, request.Tag, technical, cancellationToken));
            }
            else
            {
                skipped.Add($"Release notes for {request.Tag} "
                    + $"in {request.Repository.Owner}/{request.Repository.Name}");
            }
        }

        return (written, skipped);
    }
}
