using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Generation;
using Chartula.Core.History;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Rendering;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Pipeline;

/// <summary>
/// A first tag renders every commit up to it, and a first tag or a large range is
/// confirmed before it is read.
/// A range that is not confirmed stops before GitHub and the model are reached. The
/// unreachable stand-ins below enforce this instead of assuming it.
/// </summary>
public sealed class LargeRangeTests
{
    private static ReleasePipeline Pipeline(
        StubCommitReader commits,
        IReleasePullRequestReader pullRequests,
        IReleaseRenderer renderer,
        IReleaseRangeGate? gate = null,
        LargeRangeRule? rule = null)
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, ChangeFilterRules.Default);
        return new ReleasePipeline(
            commits,
            pullRequests,
            new FactBaseBuilder(new ReleaseChangeResolver(), filter, categorizer, labelPolicy, FactBaseDepth.TitleAndDescription),
            renderer,
            new RuleBasedFaithfulnessChecker(),
            new PassThroughThoroughChecker(),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyCustomerPageWriter(),
            new SpyReleaseNotesWriter(),
            rangeGate: gate,
            largeRangeRule: rule);
    }

    private static ReleaseRequest Request() => new("v0.1.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task A_declined_first_tag_stops_before_github_and_the_model()
    {
        RecordingGate gate = new(answer: false);
        ReleasePipeline pipeline = Pipeline(
            new StubCommitReader { FirstTag = true }, new UnreachablePullRequestReader(), new UnreachableRenderer(), gate);

        UnconfirmedRangeException error = await Assert.ThrowsAsync<UnconfirmedRangeException>(
            () => pipeline.RunAsync(Request(), PipelineMode.Preview));

        Assert.Equal("v0.1.0", error.Tag);
        Assert.Equal(1, gate.Asked);
    }

    // Nobody to ask is not a yes.
    [Fact]
    public async Task A_first_tag_without_a_gate_stops_before_github_and_the_model()
    {
        ReleasePipeline pipeline = Pipeline(
            new StubCommitReader { FirstTag = true }, new UnreachablePullRequestReader(), new UnreachableRenderer());

        await Assert.ThrowsAsync<UnconfirmedRangeException>(() => pipeline.RunAsync(Request(), PipelineMode.Preview));
    }

    [Fact]
    public async Task A_confirmed_first_tag_renders_every_commit_up_to_it()
    {
        RecordingGate gate = new(answer: true);

        ReleaseOutcome outcome = await Pipeline(
                new StubCommitReader { FirstTag = true }, new StubPullRequestReader(), new StubRenderer(), gate)
            .RunAsync(Request(), PipelineMode.Preview);

        Assert.All(outcome.Renderings, rendering => Assert.True(rendering.Success));
        Assert.True(gate.Announced?.StartsAtFirstCommit);
    }

    [Fact]
    public async Task A_range_confirmed_up_front_is_not_asked_about()
    {
        RecordingGate gate = new(answer: false);

        ReleaseOutcome outcome = await Pipeline(
                new StubCommitReader { FirstTag = true }, new StubPullRequestReader(), new StubRenderer(), gate)
            .RunAsync(Request() with { RangeConfirmed = true }, PipelineMode.Preview);

        Assert.All(outcome.Renderings, rendering => Assert.True(rendering.Success));
        Assert.Equal(0, gate.Asked);
    }

    [Fact]
    public async Task A_range_above_the_threshold_is_asked_about_although_a_previous_tag_bounds_it()
    {
        RecordingGate gate = new(answer: false);
        ReleasePipeline pipeline = Pipeline(
            new StubCommitReader { CommitCount = 4 }, new UnreachablePullRequestReader(), new UnreachableRenderer(),
            gate, new LargeRangeRule(CommitThreshold: 3));

        await Assert.ThrowsAsync<UnconfirmedRangeException>(() => pipeline.RunAsync(Request(), PipelineMode.Preview));
        Assert.Equal(1, gate.Asked);
    }

    [Fact]
    public async Task A_range_at_the_threshold_is_only_announced()
    {
        RecordingGate gate = new(answer: false);

        await Pipeline(
                new StubCommitReader { CommitCount = 3 }, new StubPullRequestReader(), new StubRenderer(),
                gate, new LargeRangeRule(CommitThreshold: 3))
            .RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(0, gate.Asked);
        Assert.Equal("v0.9.0", gate.Announced?.From);
    }

    [Fact]
    public async Task A_named_start_reaches_the_reader_and_bounds_the_range()
    {
        StubCommitReader commits = new() { FirstTag = true };
        RecordingGate gate = new(answer: false);

        await Pipeline(commits, new StubPullRequestReader(), new StubRenderer(), gate)
            .RunAsync(Request() with { Since = "abc1234" }, PipelineMode.Preview);

        Assert.Equal("abc1234", commits.Since);
        Assert.Equal(0, gate.Asked);
    }

    private sealed class RecordingGate(bool answer) : IReleaseRangeGate
    {
        public CommitRange? Announced { get; private set; }

        public int Asked { get; private set; }

        public void Announce(CommitRange range) => Announced = range;

        public Task<bool> ConfirmAsync(CommitRange range, bool sendsToModel = true, CancellationToken cancellationToken = default)
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    private sealed class UnreachablePullRequestReader : IReleasePullRequestReader
    {
        public Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
            RepositoryCoordinates repository, CommitRange range, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("GitHub was reached for a range nobody confirmed.");
    }

    private sealed class UnreachableRenderer : IReleaseRenderer
    {
        public IReadOnlyDictionary<Audience, RenderPlan> Plan(FactBase factBase, IReadOnlyCollection<Audience>? audiences = null)
            => StubPlans.Empty(audiences);

        public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
            FactBase factBase, IReadOnlyCollection<Audience>? audiences = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The model was reached for a range nobody confirmed.");
    }
}
