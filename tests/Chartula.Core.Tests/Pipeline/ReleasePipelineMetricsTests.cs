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
        => Task.FromResult(new FaithfulnessReport(findings.Length == 0, findings));
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

        // The rule-based check finds nothing here, so every thorough finding is its own.
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

        // Measurement is a side channel: it must not change what the run produces.
        Assert.All(outcome.Renderings, rendering => Assert.Contains("invented a claim", rendering.Flags));
    }

    [Fact]
    public async Task A_pipeline_without_a_metrics_sink_reports_an_empty_run()
    {
        ReleasePipeline pipeline = BuildPipeline(new PassThroughThoroughChecker(), NullRunMetrics.Instance);

        ReleaseOutcome outcome = await pipeline.RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(RunReport.Empty, outcome.Metrics);
    }
}
