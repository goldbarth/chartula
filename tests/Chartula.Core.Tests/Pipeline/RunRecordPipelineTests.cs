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
using Chartula.Core.Rendering;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Pipeline;

/// <summary>
/// #224: a run that writes keeps what it cost in a local record, so runs can be
/// compared without their terminal output.
/// </summary>
public sealed class RunRecordPipelineTests
{
    private readonly SpyRunRecordWriter _records = new();
    private readonly SpyJsonWriter _json = new();

    private ReleasePipeline BuildPipeline(IReleaseRenderer? renderer = null)
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
            renderer ?? new StubRenderer(),
            new RuleBasedFaithfulnessChecker(),
            new FindingThoroughChecker("invented a claim"),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            _json,
            new SpyMarkdownWriter(),
            new SpyCustomerPageWriter(),
            new SpyReleaseNotesWriter(),
            new RunMetrics(),
            _records);
    }

    private static ReleaseRequest Request() => new("v1.0.0", new RepositoryCoordinates("octo", "repo"));

    [Theory]
    [InlineData(PipelineMode.Generate)]
    [InlineData(PipelineMode.GenerateWithoutPublishing)]
    public async Task A_run_that_writes_records_its_audiences_and_metrics(PipelineMode mode)
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), mode);

        RunRecord record = Assert.Single(_records.Records);
        Assert.Equal("v1.0.0", record.Tag);
        Assert.Equal(mode, record.Mode);
        Assert.Equal(outcome.Renderings, record.Audiences);
        Assert.Equal(3, record.Metrics.Thorough.Flags);
        Assert.Equal("chartula-runs/run.json", outcome.RunRecord);
    }

    [Fact]
    public async Task A_preview_records_nothing()
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), PipelineMode.Preview);

        Assert.Empty(_records.Records);
        Assert.Null(outcome.RunRecord);
    }

    // The tokens of a run in which nothing rendered were spent all the same; only
    // changelog.json is held back, so an earlier good run's file survives.
    [Fact]
    public async Task A_run_in_which_no_audience_rendered_is_still_recorded()
    {
        ReleaseOutcome outcome = await BuildPipeline(
                new FailingRenderer(Audience.Technical, Audience.Customer, Audience.Product))
            .RunAsync(Request(), PipelineMode.Generate);

        RunRecord record = Assert.Single(_records.Records);
        Assert.All(record.Audiences, audience => Assert.False(audience.Success));
        Assert.Equal(0, _json.Calls);
        Assert.Empty(outcome.WrittenOutputs);
    }
}
