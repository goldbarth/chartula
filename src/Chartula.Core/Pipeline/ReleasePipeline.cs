using System.Diagnostics;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;

namespace Chartula.Core.Pipeline;

/// <summary>
/// Default <see cref="IReleasePipeline"/>. It reads the release history and pull
/// requests, builds the fact base, renders every audience, runs the rule-based and
/// thorough faithfulness checks and review, and writes the outputs.
/// <para>
/// The modes:
/// <list type="bullet">
/// <item>preview stops at the fact base: it makes no model call and writes and publishes
/// nothing, and shows what generate would render and send (#259),</item>
/// <item>generate writes and publishes,</item>
/// <item>generate-without-publishing writes the local files and leaves the release notes alone.</item>
/// </list>
/// </para>
/// <para>
/// Outputs:
/// <list type="bullet">
/// <item>The technical rendering feeds CHANGELOG.md and the release notes.</item>
/// <item>The customer rendering feeds its own page.</item>
/// <item>changelog.json stores every audience's text.</item>
/// </list>
/// </para>
/// <para>
/// The pipeline records what each faithfulness check caught, so the run reports its
/// own cost. A run that writes keeps that report in a local run record.
/// </para>
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
    ICustomerPageWriter customerPageWriter,
    IReleaseNotesWriter releaseNotesWriter,
    IRunMetrics? metrics = null,
    IRunRecordWriter? runRecordWriter = null,
    IReleaseRangeGate? rangeGate = null,
    LargeRangeRule? largeRangeRule = null,
    IChangelogPromptBuilder? promptBuilder = null,
    IRunProgress? progress = null) : IReleasePipeline
{
    /// <summary>
    /// What <c>CHANGELOG.md</c> and the release notes say for a release whose changes reach
    /// no technical reader (#307), in place of a bare heading and an empty body, which read
    /// like a run that lost its text. Chartula writes it, not the model, so the release
    /// still costs no model call. It claims only what an empty technical rendering proves:
    /// every change that is new, fixed, faster, breaking or labelled visible reaches it.
    /// </summary>
    public const string NothingForTechnicalReaders =
        "This release has no features, fixes, performance improvements or breaking changes.";

    private readonly IRunMetrics _metrics = metrics ?? NullRunMetrics.Instance;
    private readonly IRunProgress _progress = progress ?? NullRunProgress.Instance;
    private readonly LargeRangeRule _largeRangeRule = largeRangeRule ?? LargeRangeRule.Default;
    private readonly IChangelogPromptBuilder _promptBuilder = promptBuilder ?? new ChangelogPromptBuilder();

    public async Task<ReleaseOutcome> RunAsync(
        ReleaseRequest request,
        PipelineMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await RunStepsAsync(request, mode, cancellationToken);
        }
        finally
        {
            // Also when a step failed: it stays the last line shown, above the error.
            _progress.Complete();
        }
    }

    private async Task<ReleaseOutcome> RunStepsAsync(
        ReleaseRequest request,
        PipelineMode mode,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();

        CommitRange range = await commitReader.ReadReleaseCommitsAsync(request.Tag, request.Since, cancellationToken);

        // Ask before the first API request. A large range costs a GitHub request per commit
        // and sends every change to the model, and a run nobody meant should cost nothing.
        rangeGate?.Announce(range);
        if (!request.RangeConfirmed
            && _largeRangeRule.Applies(range)
            && (rangeGate is null
                || !await rangeGate.ConfirmAsync(range, sendsToModel: mode != PipelineMode.Preview, cancellationToken)))
        {
            throw new UnconfirmedRangeException(range.ToTag);
        }

        // Publishing is the last step, after every model call. A token that may not publish
        // is found here instead, while stopping still costs nothing.
        if (mode == PipelineMode.Generate)
        {
            await EnsureCanPublishAsync(request, cancellationToken);
        }

        // One request per commit, which the reader counts off.
        _progress.Begin(new ProgressStep(RunStep.ReadingPullRequests), range.Commits.Count);
        IReadOnlyList<PullRequestInfo> pullRequests =
            await pullRequestReader.GetMergedPullRequestsAsync(request.Repository, range, cancellationToken);
        CuratedRelease curated = factBaseBuilder.Curate(range, pullRequests);
        FactBase factBase = curated.Facts;
        DirectCommits direct = DirectCommits.Of(range, pullRequests);
        _metrics.RecordReleaseScope(new ReleaseScope(
            range.Commits.Count,
            pullRequests.Count,
            factBase.Changes.Count,
            factBase.Changes.Count(static change => !string.IsNullOrWhiteSpace(change.Description)),
            factBase.Changes.Sum(static change => (long)(change.Description?.Length ?? 0)),
            direct.Commits.Count + direct.SkippedMerges,
            direct.SkippedMerges));

        // Every fact is decided here, before the model. A preview stops and shows them.
        if (mode == PipelineMode.Preview)
        {
            _metrics.RecordRunDuration(Stopwatch.GetElapsedTime(started));
            return new ReleaseOutcome(request.Tag, mode, [], [], _metrics.Snapshot())
            {
                Preview = Preview(curated, request.Audiences),
            };
        }

        IReadOnlyDictionary<Audience, ChangelogGenerationResult> rendered =
            await renderer.RenderAsync(factBase, request.Audiences, cancellationToken);

        List<AudienceOutcome> outcomes = [];
        Dictionary<Audience, string> finalTexts = [];
        Dictionary<Audience, string?> descriptions = [];
        foreach ((Audience audience, ChangelogGenerationResult result) in rendered.OrderBy(entry => entry.Key))
        {
            if (!result.IsSuccess)
            {
                outcomes.Add(new AudienceOutcome(audience, Success: false, Text: null, Flags: [], result.Error));
                continue;
            }

            string text = result.Text ?? string.Empty;

            // Check the description together with its text. The model wrote it from the
            // same facts, so its claims need the same check as any other.
            _progress.Begin(new ProgressStep(RunStep.Checking, audience));
            IReadOnlyList<FaithfulnessFlag> flags =
                await CollectFlagsAsync(Checkable(result), factBase, cancellationToken);

            ReviewDecision decision =
                await reviewCoordinator.ReviewAsync(new ReviewItem(audience, text, flags), cancellationToken);

            finalTexts[audience] = decision.Text;
            descriptions[audience] = result.Description;
            outcomes.Add(new AudienceOutcome(audience, Success: true, decision.Text, flags, Error: null)
            {
                Description = result.Description,
            });
        }

        // The run duration covers everything the run waited on: history, GitHub and
        // every model call. Writing the results takes no time worth reporting.
        _metrics.RecordRunDuration(Stopwatch.GetElapsedTime(started));
        RunReport report = _metrics.Snapshot();

        // All model calls are done, so the record is complete here.
        // Write it even when nothing rendered, because those tokens were still spent.
        string? runRecord = null;
        if (runRecordWriter is not null)
        {
            runRecord = await runRecordWriter.WriteAsync(
                new RunRecord(request.Tag, request.Repository, mode, outcomes, report)
                {
                    Range = range,
                    Since = string.IsNullOrWhiteSpace(request.Since) ? null : request.Since.Trim(),
                    Facts = factBase,
                },
                cancellationToken);
        }

        IReadOnlyList<string> written = [];
        IReadOnlyList<string> skipped = [];
        string? publishFailure = null;
        // Write no outputs when no audience rendered. Writing the fact base alone would
        // replace the renderings of an earlier, good run with none, and would look
        // like a run that produced something.
        if (finalTexts.Count > 0)
        {
            (written, skipped, publishFailure) = await WriteOutputsAsync(
                request, range, factBase, finalTexts, descriptions, mode, cancellationToken);
        }

        return new ReleaseOutcome(request.Tag, mode, outcomes, written, report)
        {
            SkippedOutputs = skipped,
            PublishFailure = publishFailure,
            RunRecord = runRecord,
        };
    }

    // The same plans the renderer would send, measured with the same prompt builder,
    // so what a preview reports is what generate sends.
    private ReleasePreview Preview(CuratedRelease curated, IReadOnlyCollection<Audience>? audiences)
    {
        IReadOnlyDictionary<Audience, RenderPlan> plans = renderer.Plan(curated.Facts, audiences);

        List<AudiencePreview> sent = [];
        foreach ((Audience audience, RenderPlan plan) in plans)
        {
            int characters = 0;
            if (plan.Entries.Count > 0)
            {
                ChangelogPrompt prompt = _promptBuilder.BuildRephrasePrompt(plan.Facts, audience);
                characters = prompt.System.Length + prompt.User.Length;
            }

            sent.Add(new AudiencePreview(audience, plan.Entries.Count, characters));
        }

        List<PreviewFact> facts = [.. curated.Facts.Changes.Select(fact => new PreviewFact(
            fact, [.. plans.Keys.Where(audience => GroundedFactsFactory.Reaches(fact, audience))]))];
        return new ReleasePreview(facts, curated.Dropped, sent, thoroughChecker.Enabled);
    }

    private async Task EnsureCanPublishAsync(ReleaseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await releaseNotesWriter.EnsureCanWriteAsync(
                request.Repository, request.Tag, request.ReplacePublished, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"""
                {ex.Message.TrimEnd()}
                  The run stopped before any model call. --no-publish writes the files without publishing them.
                """,
                ex);
        }
    }

    private async Task<IReadOnlyList<FaithfulnessFlag>> CollectFlagsAsync(
        string text, FactBase factBase, CancellationToken cancellationToken)
    {
        IReadOnlyList<FaithfulnessFlag> ruleBased = ruleBasedChecker.Check(text, factBase).UnsupportedClaims;
        FaithfulnessReport thorough = await thoroughChecker.CheckAsync(text, factBase, cancellationToken);

        // Record both findings together, so the report can tell what the paid check
        // caught beyond the free one.
        bool thoroughEvaluated = thorough.Status != FaithfulnessCheckStatus.NotEvaluated;
        _metrics.RecordFaithfulnessChecks(
            [.. ruleBased.Select(static flag => flag.Text)],
            [.. thorough.UnsupportedClaims.Select(static flag => flag.Text)],
            thoroughEvaluated);

        List<FaithfulnessFlag> flags = [.. ruleBased, .. thorough.UnsupportedClaims];

        // An unreadable thorough check leaves the text unverified. Without its own flag,
        // it would look like a check that found nothing.
        if (!thoroughEvaluated)
        {
            // Trim the period: a reason from a failed model call may already end with one.
            flags.Add(new FaithfulnessFlag($"The thorough check could not be evaluated: {thorough.Reason?.TrimEnd('.')}."));
        }

        return flags;
    }

    private async Task<(IReadOnlyList<string> Written, IReadOnlyList<string> Skipped, string? PublishFailure)> WriteOutputsAsync(
        ReleaseRequest request,
        CommitRange range,
        FactBase factBase,
        IReadOnlyDictionary<Audience, string> finalTexts,
        IReadOnlyDictionary<Audience, string?> descriptions,
        PipelineMode mode,
        CancellationToken cancellationToken)
    {
        List<string> written = [];
        List<string> skipped = [];
        string? publishFailure = null;

        written.Add(await jsonWriter.WriteAsync(factBase, finalTexts, cancellationToken));

        // Write the customer page even with --no-publish: a local file publishes nothing.
        // The page uses the published serialisation, so a person can publish it as is.
        // An empty customer text produces no page, not an empty page.
        if (finalTexts.TryGetValue(Audience.Customer, out string? customer)
            && !string.IsNullOrWhiteSpace(customer))
        {
            descriptions.TryGetValue(Audience.Customer, out string? description);
            written.Add(await customerPageWriter.WriteAsync(
                new CustomerPage(
                    request.Tag,
                    range.TaggedAt,
                    description,
                    // No labels reach the fact base yet (issue #98), and the format
                    // says an absent field is the correct output, not a defect.
                    Tags: [],
                    customer),
                cancellationToken));
        }

        if (finalTexts.TryGetValue(Audience.Technical, out string? technical))
        {
            // Only the two technical outputs get the sentence. changelog.json keeps the
            // empty rendering, which its schema documents as "no change for that audience".
            if (string.IsNullOrWhiteSpace(technical))
            {
                technical = NothingForTechnicalReaders;
            }

            written.Add(await markdownWriter.WriteAsync(request.Tag, range.TaggedAt, technical, cancellationToken));

            // CHANGELOG.md is always written. Only publishing the release notes can be
            // skipped, and a skipped publication is listed, so a run that published
            // nothing says so.
            if (mode == PipelineMode.Generate)
            {
                // Publishing the release notes is the only write that leaves the machine,
                // and the last one. A failure here is reported together with what was
                // already written.
                try
                {
                    written.Add(await releaseNotesWriter.WriteAsync(
                        request.Repository, request.Tag, technical, request.ReplacePublished, cancellationToken));
                }
                catch (InvalidOperationException ex)
                {
                    publishFailure = ex.Message;
                }
            }
            else
            {
                skipped.Add($"Release notes for {request.Tag} "
                    + $"in {request.Repository.Owner}/{request.Repository.Name}");
            }
        }

        return (written, skipped, publishFailure);
    }

    /// <summary>
    /// The text of a rendering that the faithfulness checks see: description and body
    /// together, so no text the model wrote escapes the check by sitting in another field.
    /// </summary>
    private static string Checkable(ChangelogGenerationResult result)
        => string.IsNullOrWhiteSpace(result.Description)
            ? result.Text ?? string.Empty
            : result.Description + "\n\n" + (result.Text ?? string.Empty);
}
