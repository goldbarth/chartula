using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Pipeline;

/// <summary>
/// The customer rendering leaving a run as a file of its own. Until it did, the
/// audience the tool exists for had no output a person could publish - it lived
/// only as a field inside changelog.json.
/// </summary>
public sealed class CustomerPageOutputTests
{
    private readonly SpyCustomerPageWriter _customerPage = new();

    private ReleasePipeline BuildPipeline(
        IReleaseRenderer? renderer = null,
        IThoroughFaithfulnessChecker? thorough = null,
        DateOnly? taggedAt = null)
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, ChangeFilterRules.Default);
        FactBaseBuilder factBaseBuilder = new(
            new ReleaseChangeResolver(), filter, categorizer, labelPolicy, FactBaseDepth.TitleAndDescription);

        return new ReleasePipeline(
            new StubCommitReader(taggedAt),
            new StubPullRequestReader(),
            factBaseBuilder,
            renderer ?? new StubRenderer(),
            new RuleBasedFaithfulnessChecker(),
            thorough ?? new PassThroughThoroughChecker(),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            _customerPage,
            new SpyReleaseNotesWriter());
    }

    private static ReleaseRequest Request() => new("v1.0.0", new RepositoryCoordinates("octo", "repo"));

    [Fact]
    public async Task A_run_writes_the_customer_rendering_as_a_file_of_its_own()
    {
        ReleaseOutcome outcome = await BuildPipeline().RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal(1, _customerPage.Calls);
        Assert.Contains("release-v1.0.0.md", outcome.WrittenOutputs);
    }

    [Fact]
    public async Task The_page_carries_the_rendering_the_run_settled_on()
    {
        await BuildPipeline().RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal("- Search is here.", _customerPage.LastPage!.Body);
        Assert.Equal("v1.0.0", _customerPage.LastPage.Tag);
    }

    [Fact]
    public async Task The_front_matter_dates_the_page_from_the_tag()
    {
        await BuildPipeline(taggedAt: new DateOnly(2026, 9, 3)).RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal(new DateOnly(2026, 9, 3), _customerPage.LastPage!.PublishedAt);
    }

    [Fact]
    public async Task The_description_written_with_the_text_reaches_the_page()
    {
        await BuildPipeline(new CustomerRenderer("- Search is here.", "A release about finding things."))
            .RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal("A release about finding things.", _customerPage.LastPage!.Description);
    }

    [Fact]
    public async Task No_tags_reach_the_page_while_no_labels_reach_the_fact_base()
    {
        // Issue #98. The format says an absent field is the correct output here,
        // so the page carries none rather than something stood in for them.
        await BuildPipeline().RunAsync(Request(), PipelineMode.Generate);

        Assert.Empty(_customerPage.LastPage!.Tags);
    }

    [Fact]
    public async Task The_page_is_written_under_no_publish_like_the_other_local_outputs()
    {
        // Writing a file a person could publish is not publishing it.
        ReleaseOutcome outcome =
            await BuildPipeline().RunAsync(Request(), PipelineMode.GenerateWithoutPublishing);

        Assert.Equal(1, _customerPage.Calls);
        Assert.Contains("release-v1.0.0.md", outcome.WrittenOutputs);
        Assert.DoesNotContain(outcome.SkippedOutputs, skipped => skipped.Contains("release-v1.0.0.md"));
    }

    [Fact]
    public async Task Preview_writes_no_page()
    {
        await BuildPipeline().RunAsync(Request(), PipelineMode.Preview);

        Assert.Equal(0, _customerPage.Calls);
    }

    [Fact]
    public async Task A_release_with_nothing_for_customers_produces_no_page_rather_than_an_empty_one()
    {
        await BuildPipeline(new CustomerRenderer(string.Empty)).RunAsync(Request(), PipelineMode.Generate);

        Assert.Equal(0, _customerPage.Calls);
    }

    [Fact]
    public async Task The_faithfulness_check_covers_the_description_as_it_covers_the_text()
    {
        RecordingThoroughChecker thorough = new();

        await BuildPipeline(
                new CustomerRenderer("- Search is here.", "A release about finding things."),
                thorough)
            .RunAsync(Request(), PipelineMode.Generate);

        // A sentence the model wrote is a sentence the check has to be able to
        // answer for, whichever field carries it out of the run.
        Assert.Contains(thorough.Checked, text => text.Contains("A release about finding things."));
    }

    [Fact]
    public async Task A_preview_shows_the_description_it_would_publish()
    {
        ReleaseOutcome outcome =
            await BuildPipeline(new CustomerRenderer("- Search is here.", "A release about finding things."))
                .RunAsync(Request(), PipelineMode.Preview);

        AudienceOutcome customer = Assert.Single(
            outcome.Renderings, rendering => rendering.Audience == Audience.Customer);
        Assert.Equal("A release about finding things.", customer.Description);
    }
}
