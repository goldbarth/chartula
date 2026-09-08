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
/// A run renders the audiences it was asked for, and writes only the outputs
/// those renderings are made of. Nothing here is a special case: the pipeline
/// already writes each output only when its rendering is present, so leaving an
/// audience out leaves its file out.
/// </summary>
public sealed class AudienceSelectionPipelineTests
{
    private readonly SpyMarkdownWriter _markdown = new();
    private readonly SpyCustomerPageWriter _customerPage = new();
    private readonly StubRenderer _renderer = new();

    private ReleasePipeline BuildPipeline()
    {
        ConventionalCommitCategorizer categorizer = new();
        LabelRulePolicy labelPolicy = new(LabelRules.None);
        ChangeFilter filter = new(categorizer, labelPolicy, ChangeFilterRules.Default);
        FactBaseBuilder factBaseBuilder = new(
            new ReleaseChangeResolver(), filter, categorizer, labelPolicy, FactBaseDepth.TitleAndDescription);

        return new ReleasePipeline(
            new StubCommitReader(null),
            new StubPullRequestReader(),
            factBaseBuilder,
            _renderer,
            new RuleBasedFaithfulnessChecker(),
            new PassThroughThoroughChecker(),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            _markdown,
            _customerPage,
            new SpyReleaseNotesWriter());
    }

    private static ReleaseRequest Request(params Audience[] audiences)
        => new("v1.0.0", new RepositoryCoordinates("octo", "repo"))
        {
            Audiences = audiences.Length == 0 ? null : audiences,
        };

    [Fact]
    public async Task A_request_naming_no_audience_renders_all_of_them()
    {
        await BuildPipeline().RunAsync(Request(), PipelineMode.Generate);

        Assert.Null(_renderer.Asked);
        Assert.Equal(1, _markdown.Calls);
        Assert.NotNull(_customerPage.LastPage);
    }

    [Fact]
    public async Task A_request_for_the_customer_alone_reaches_the_renderer()
    {
        await BuildPipeline().RunAsync(Request(Audience.Customer), PipelineMode.Generate);

        Assert.Equal([Audience.Customer], _renderer.Asked);
    }

    [Fact]
    public async Task An_output_whose_audience_was_not_rendered_is_not_written()
    {
        // CHANGELOG.md is made of the technical rendering. Without it there is
        // nothing to write, and writing the file empty would replace a good one.
        await BuildPipeline().RunAsync(Request(Audience.Customer), PipelineMode.Generate);

        Assert.Equal(0, _markdown.Calls);
        Assert.NotNull(_customerPage.LastPage);
    }

    [Fact]
    public async Task The_page_is_left_alone_when_the_customer_was_not_rendered()
    {
        await BuildPipeline().RunAsync(Request(Audience.Technical), PipelineMode.Generate);

        Assert.Equal(1, _markdown.Calls);
        Assert.Null(_customerPage.LastPage);
    }
}
