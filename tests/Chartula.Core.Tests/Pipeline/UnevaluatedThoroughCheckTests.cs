using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Faithfulness;
using Chartula.Core.Filtering;
using Chartula.Core.Labeling;
using Chartula.Core.Llm;
using Chartula.Core.Facts;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.Prompting;
using Chartula.Core.PullRequests;
using Chartula.Core.Review;
using Chartula.Core.Tests.Llm;

namespace Chartula.Core.Tests.Pipeline;

/// <summary>
/// A whole run whose thorough check answers with something that is not a verdict. Only
/// the chat client is a stand-in; the model, the checker and the pipeline are the real
/// ones, because the bug this pins was invisible at every level above the parse.
/// </summary>
public sealed class UnevaluatedThoroughCheckTests
{
    private readonly RunMetrics _metrics = new();

    private ReleasePipeline BuildPipeline(string providerAnswer)
    {
        ChatModel model = new(
            new StubChatClient(providerAnswer), new ChangelogPromptBuilder(), new ChatModelOptions(), _metrics);

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
            new ThoroughFaithfulnessChecker(model, new ThoroughFaithfulnessOptions(Enabled: true)),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyReleaseNotesWriter(),
            _metrics);
    }

    private Task<ReleaseOutcome> RunAsync(string providerAnswer)
        => BuildPipeline(providerAnswer).RunAsync(
            new ReleaseRequest("v1.0.0", new RepositoryCoordinates("octo", "repo")), PipelineMode.Preview);

    [Fact]
    public async Task Every_audience_is_flagged_when_the_check_cannot_be_read()
    {
        ReleaseOutcome outcome = await RunAsync("Sorry, I cannot produce JSON here.");

        Assert.All(outcome.Renderings, audience =>
            Assert.Contains(audience.Flags, flag => flag.Contains("could not be evaluated")));
    }

    [Fact]
    public async Task The_run_metrics_count_the_unreadable_checks_apart_from_clean_ones()
    {
        ReleaseOutcome outcome = await RunAsync("Sorry, I cannot produce JSON here.");

        // Three runs that verified nothing. Counting them as "0 with findings" is what
        // made a failed check read as a clean one.
        Assert.Equal(3, outcome.Metrics.Thorough.Runs);
        Assert.Equal(3, outcome.Metrics.ThoroughNotEvaluated);
        Assert.Equal(0, outcome.Metrics.Thorough.RunsWithFindings);
    }

    [Fact]
    public async Task The_run_summary_says_the_checks_verified_nothing()
    {
        ReleaseOutcome outcome = await RunAsync("Sorry, I cannot produce JSON here.");

        Assert.Contains("came back unreadable and verified nothing", RunReportFormatter.Format(outcome.Metrics));
    }

    [Fact]
    public async Task A_readable_verdict_flags_nothing_extra_and_counts_as_evaluated()
    {
        ReleaseOutcome outcome = await RunAsync("""{"isFaithful":true,"unsupportedClaims":[]}""");

        Assert.All(outcome.Renderings, audience => Assert.Empty(audience.Flags));
        Assert.Equal(0, outcome.Metrics.ThoroughNotEvaluated);
        Assert.DoesNotContain("unreadable", RunReportFormatter.Format(outcome.Metrics));
    }
}
