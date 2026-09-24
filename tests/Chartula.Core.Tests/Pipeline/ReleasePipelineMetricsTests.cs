using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Pipeline;

/// <summary>A thorough check that returns fixed findings, standing in for the LLM pass.</summary>
internal sealed class FindingThoroughChecker(params string[] findings) : IThoroughFaithfulnessChecker
{
    public Task<FaithfulnessReport> CheckAsync(
        string output, FactBase factBase, CancellationToken cancellationToken = default)
        => Task.FromResult(FaithfulnessReport.Checked([.. findings.Select(static finding => new FaithfulnessFlag(finding))]));
}

public sealed class ReleasePipelineMetricsTests
{
    private static ReleasePipeline BuildPipeline(IThoroughFaithfulnessChecker thorough, IRunMetrics metrics)
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, ChangeFilterRules.Default);
        FactBaseBuilder factBaseBuilder = new(
            new ReleaseChangeResolver(), filter, categorizer, labelPolicy, FactBaseDepth.TitleAndDescription);

        return new ReleasePipeline(
            new StubCommitReader(),
            new StubPullRequestReader(),
            factBaseBuilder,
            new StubRenderer(),
            new RuleBasedFaithfulnessChecker(),
            thorough,
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyCustomerPageWriter(),
            new SpyReleaseNotesWriter(),
            metrics);
    }

    private static ReleaseRequest Request() => new("v1.0.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task A_run_records_one_check_pass_per_rendered_audience()
    {
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(new PassThroughThoroughChecker(), metrics)
            .RunAsync(Request(), PipelineMode.Preview);

        // Three audiences are rendered, so each check runs three times.
        Assert.Equal(3, outcome.Renderings.Count);
        Assert.Equal(3, outcome.Metrics.RuleBased.Runs);
        Assert.Equal(3, outcome.Metrics.Thorough.Runs);
    }

    [Fact]
    public async Task The_outcome_reports_what_the_thorough_check_caught_on_its_own()
    {
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(new FindingThoroughChecker("invented a claim"), metrics)
            .RunAsync(Request(), PipelineMode.Preview);

        // The rule-based check finds nothing here, so every thorough finding counts as thorough-only.
        Assert.Equal(0, outcome.Metrics.RuleBased.Flags);
        Assert.Equal(3, outcome.Metrics.Thorough.Flags);
        Assert.Equal(3, outcome.Metrics.Thorough.RunsWithFindings);
        Assert.Equal(3, outcome.Metrics.ThoroughOnlyFlags);
    }

    [Fact]
    public async Task Findings_from_both_checks_still_reach_the_rendering()
    {
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(new FindingThoroughChecker("invented a claim"), metrics)
            .RunAsync(Request(), PipelineMode.Preview);

        // Metrics must not change what the run produces.
        Assert.All(outcome.Renderings, rendering => Assert.Contains(new FaithfulnessFlag("invented a claim"), rendering.Flags));
    }

    [Fact]
    public async Task A_pipeline_without_a_metrics_sink_reports_an_empty_run()
    {
        ReleasePipeline pipeline = BuildPipeline(new PassThroughThoroughChecker(), NullRunMetrics.Instance);

        ReleaseOutcome outcome = await pipeline.RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(RunReport.Empty, outcome.Metrics);
    }

    // #128: the whole run's duration, so a slow run is visible before reading its calls.
    [Fact]
    public async Task A_run_records_how_long_it_took()
    {
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(new PassThroughThoroughChecker(), metrics)
            .RunAsync(Request(), PipelineMode.Preview);

        Assert.NotNull(outcome.Metrics.Duration);
    }

    // The context for the run's token counts: the size of the release, and how much
    // description text the model got beyond the titles.
    [Fact]
    public async Task A_run_records_how_much_release_it_worked_on()
    {
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(new PassThroughThoroughChecker(), metrics)
            .RunAsync(Request(), PipelineMode.Preview);

        // One commit, one pull request, one fact described as "Adds search.".
        Assert.Equal(new ReleaseScope(1, 1, 1, 1, 12), outcome.Metrics.Scope);
    }
}
