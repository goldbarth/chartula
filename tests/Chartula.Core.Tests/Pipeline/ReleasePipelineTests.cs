using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Facts;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Pipeline;

public sealed class ReleasePipelineTests
{
    private readonly SpyJsonWriter _json = new();
    private readonly SpyMarkdownWriter _markdown = new();
    private readonly SpyReleaseNotesWriter _releaseNotes = new();

    private ReleasePipeline BuildPipeline()
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
            new PassThroughThoroughChecker(),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            _json,
            _markdown,
            _releaseNotes);
    }

    private static ReleaseRequest Request() => new("v1.0.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task Preview_writes_and_publishes_nothing()
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(PipelineMode.Preview, outcome.Mode);
        Assert.Empty(outcome.WrittenOutputs);
        Assert.Equal(0, _json.Calls);
        Assert.Equal(0, _markdown.Calls);
        Assert.Equal(0, _releaseNotes.Calls);
    }

    [Fact]
    public async Task Preview_still_produces_all_three_audience_renderings()
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(3, outcome.Renderings.Count);
        Assert.All(outcome.Renderings, r => Assert.True(r.Success));
        Assert.Contains(outcome.Renderings, r => r.Audience == Audience.Customer && r.Text == "- Search is here.");
    }

    [Fact]
    public async Task Generate_writes_all_outputs()
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal(PipelineMode.Generate, outcome.Mode);
        Assert.Equal(1, _json.Calls);
        Assert.Equal(1, _markdown.Calls);
        Assert.Equal(1, _releaseNotes.Calls);
        Assert.Contains("changelog.json", outcome.WrittenOutputs);
        Assert.Contains("CHANGELOG.md", outcome.WrittenOutputs);
    }
}
