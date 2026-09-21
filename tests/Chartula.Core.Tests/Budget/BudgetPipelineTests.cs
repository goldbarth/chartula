using Chartula.Core.Budget;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Pipeline;
using Chartula.Core.Prompting;
using Chartula.Core.PullRequests;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Tests.Fixtures;
using Chartula.Core.Tests.Pipeline;

namespace Chartula.Core.Tests.Budget;

/// <summary>
/// A refused run costs no tokens. The model behind both the renderer and the check
/// throws if reached; the generator would swallow that into a failed audience, so a
/// budget consulted too late shows up as a run that did not throw.
/// </summary>
public sealed class BudgetPipelineTests
{
    private static ReleasePipeline Pipeline(FactBase factBase, IRunBudget budget)
    {
        UnreachableChangelogModel model = new();
        return new ReleasePipeline(
            new StubCommitReader(),
            new StubPullRequestReader(),
            new FixtureFactBaseBuilder(factBase),
            new ReleaseRenderer(new ReleaseChangelogGenerator(model, new ChangelogFormatter())),
            new RuleBasedFaithfulnessChecker(),
            new ThoroughFaithfulnessChecker(model, new ThoroughFaithfulnessOptions(true)),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyCustomerPageWriter(),
            new SpyReleaseNotesWriter(),
            estimator: new RunEstimator(
                new ChangelogPromptBuilder(), new ChangelogFormatter(), new ChatModelOptions(), new ThoroughFaithfulnessOptions(true)),
            budget: budget);
    }

    [Fact]
    public async Task A_refused_run_reaches_no_model()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.Typical);
        RefusingBudget budget = new();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(factBase, budget).RunAsync(
                new ReleaseRequest(factBase.Tag, new RepositoryCoordinates("octo", "repo")), PipelineMode.Preview));

        Assert.Equal("over budget", error.Message);
        Assert.Equal(6, budget.Seen!.Calls.Count);
    }

    private sealed class RefusingBudget : IRunBudget
    {
        public RunEstimate? Seen { get; private set; }

        public void Approve(RunEstimate estimate)
        {
            Seen = estimate;
            throw new InvalidOperationException("over budget");
        }
    }
}
