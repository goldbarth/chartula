using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Releases;
using Chartula.Core.Rendering;
using Chartula.Core.Review;

namespace Chartula.Core.Tests.Pipeline;

public sealed class ReleasePipelineTests
{
    private readonly SpyJsonWriter _json = new();
    private readonly SpyMarkdownWriter _markdown = new();
    private readonly SpyCustomerPageWriter _customerPage = new();
    private readonly SpyReleaseNotesWriter _releaseNotes = new();

    private ReleasePipeline BuildPipeline(IReleaseRenderer? renderer = null, IReleaseNotesWriter? releaseNotes = null)
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
            new PassThroughThoroughChecker(),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            _json,
            _markdown,
            _customerPage,
            releaseNotes ?? _releaseNotes);
    }

    private static ReleaseRequest Request() => new("v1.0.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task A_run_in_which_no_audience_rendered_writes_and_publishes_nothing()
    {
        ReleaseOutcome outcome = await BuildPipeline(
                new FailingRenderer(Audience.Technical, Audience.Customer, Audience.Product))
            .RunAsync(Request(), PipelineMode.Generate);

        // An earlier run's changelog.json must not be replaced by one with no renderings.
        Assert.Empty(outcome.WrittenOutputs);
        Assert.Equal(0, _json.Calls);
        Assert.Equal(0, _markdown.Calls);
        Assert.Equal(0, _customerPage.Calls);
        Assert.Equal(0, _releaseNotes.Calls);
    }

    [Fact]
    public async Task A_run_in_which_some_audiences_failed_writes_what_rendered()
    {
        ReleaseOutcome outcome = await BuildPipeline(new FailingRenderer(Audience.Technical))
            .RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal(1, _json.Calls);
        Assert.Equal(1, _customerPage.Calls);
        Assert.Equal(0, _markdown.Calls);
        Assert.Equal(0, _releaseNotes.Calls);
    }

    // #219: publishing is the last write, so a refusal there leaves the files written;
    // the outcome has to carry both, not trade the list for the error.
    [Fact]
    public async Task A_refused_publication_keeps_the_files_the_run_wrote()
    {
        ReleaseOutcome outcome = await BuildPipeline(releaseNotes: new RefusingReleaseNotesWriter("refused"))
            .RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal("refused", outcome.PublishFailure);
        Assert.Contains("CHANGELOG.md", outcome.WrittenOutputs);
        Assert.Equal(1, _json.Calls);
        Assert.Equal(1, _customerPage.Calls);
    }

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
        Assert.Empty(outcome.SkippedOutputs);
    }

    [Fact]
    public async Task Without_publishing_it_writes_the_files_and_leaves_the_release_notes_alone()
    {
        ReleaseOutcome outcome =
            await BuildPipeline().RunAsync(Request(), PipelineMode.GenerateWithoutPublishing);

        Assert.Equal(1, _json.Calls);
        Assert.Equal(1, _markdown.Calls);
        Assert.Equal(0, _releaseNotes.Calls);
        Assert.Contains("changelog.json", outcome.WrittenOutputs);
        Assert.Contains("CHANGELOG.md", outcome.WrittenOutputs);
    }

    [Fact]
    public async Task A_skipped_publication_is_named_rather_than_silent()
    {
        ReleaseOutcome outcome =
            await BuildPipeline().RunAsync(Request(), PipelineMode.GenerateWithoutPublishing);

        // The release the run did not touch has to be readable from the outcome,
        // otherwise "wrote two of three outputs" looks like a complete run.
        Assert.Equal("Release notes for v1.0.0 in octo/repo", Assert.Single(outcome.SkippedOutputs));
    }
}
