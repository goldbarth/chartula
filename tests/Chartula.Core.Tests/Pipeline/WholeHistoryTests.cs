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
/// A first tag's range is all history. The operator decides where the release starts,
/// so a run without that decision stops.
/// It stops before GitHub and the model are reached. The unreachable stand-ins below
/// enforce this instead of assuming it.
/// </summary>
public sealed class WholeHistoryTests
{
    private static ReleasePipeline Pipeline(
        StubCommitReader commits, IReleasePullRequestReader pullRequests, IReleaseRenderer renderer)
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
            new SpyReleaseNotesWriter());
    }

    private static ReleaseRequest Request() => new("v0.1.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task A_first_tag_without_a_start_stops_before_github_and_the_model()
    {
        ReleasePipeline pipeline = Pipeline(
            new StubCommitReader { WholeHistory = true }, new UnreachablePullRequestReader(), new UnreachableRenderer());

        WholeHistoryException error = await Assert.ThrowsAsync<WholeHistoryException>(
            () => pipeline.RunAsync(Request(), PipelineMode.Preview));

        Assert.Equal("v0.1.0", error.Tag);
        Assert.Equal(1, error.CommitCount);
    }

    [Fact]
    public async Task The_whole_history_renders_when_asked_for()
    {
        ReleaseOutcome outcome = await Pipeline(
                new StubCommitReader { WholeHistory = true }, new StubPullRequestReader(), new StubRenderer())
            .RunAsync(Request() with { WholeHistory = true }, PipelineMode.Preview);

        Assert.All(outcome.Renderings, rendering => Assert.True(rendering.Success));
    }

    [Fact]
    public async Task A_named_start_reaches_the_reader_and_bounds_the_range()
    {
        StubCommitReader commits = new() { WholeHistory = true };

        await Pipeline(commits, new StubPullRequestReader(), new StubRenderer())
            .RunAsync(Request() with { Since = "abc1234" }, PipelineMode.Preview);

        Assert.Equal("abc1234", commits.Since);
    }

    private sealed class UnreachablePullRequestReader : IReleasePullRequestReader
    {
        public Task<IReadOnlyList<PullRequestInfo>> GetMergedPullRequestsAsync(
            RepositoryCoordinates repository, CommitRange range, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("GitHub was reached for a range nobody asked for.");
    }

    private sealed class UnreachableRenderer : IReleaseRenderer
    {
        public Task<IReadOnlyDictionary<Audience, ChangelogGenerationResult>> RenderAsync(
            FactBase factBase, IReadOnlyCollection<Audience>? audiences = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The model was reached for a range nobody asked for.");
    }
}
