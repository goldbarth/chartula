using Chartula.Core.Facts;
using Chartula.Core.Faithfulness;
using Chartula.Core.Formatting;
using Chartula.Core.Generation;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Pipeline;
using Chartula.Core.PullRequests;
using Chartula.Core.Rendering;
using Chartula.Core.Review;
using Chartula.Core.Serialization;
using Chartula.Core.Tests.Pipeline;

namespace Chartula.Core.Tests.Fixtures;

/// <summary>
/// Replays the stored fact bases through the real pipeline. Everything downstream of the
/// facts is the production type - generator, renderer, formatter, both checks, review and
/// the writers. Only the model is a stand-in, so these tests exercise the pipeline end to
/// end at no token cost and can be run as often as wanted.
/// </summary>
public sealed class FixturePipelineTests
{
    private static ReleasePipeline BuildPipeline(
        FactBase factBase,
        IChangelogModel model,
        IRunMetrics metrics,
        bool thoroughEnabled = true)
        => new(
            new StubCommitReader(),
            new StubPullRequestReader(),
            new FixtureFactBaseBuilder(factBase),
            new ReleaseRenderer(new ReleaseChangelogGenerator(model, new ChangelogFormatter())),
            new RuleBasedFaithfulnessChecker(),
            new ThoroughFaithfulnessChecker(model, new ThoroughFaithfulnessOptions(thoroughEnabled)),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyReleaseNotesWriter(),
            metrics);

    private static ReleaseRequest Request(FactBase factBase)
        => new(factBase.Tag, new RepositoryCoordinates("owner", "repo"));

    [Theory]
    [MemberData(nameof(FactBaseFixture.AllAsTheoryData), MemberType = typeof(FactBaseFixture))]
    public async Task Every_fixture_runs_through_the_whole_pipeline(string fixture)
    {
        FactBase factBase = FactBaseFixture.Load(fixture);
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(factBase, new EchoingChangelogModel(), metrics)
            .RunAsync(Request(factBase), PipelineMode.Generate);

        Assert.Equal(factBase.Tag, outcome.Tag);
        Assert.All(outcome.Renderings, rendering => Assert.True(rendering.Success, rendering.Error));
        Assert.Equal(3, outcome.Renderings.Count);
    }

    [Theory]
    [MemberData(nameof(FactBaseFixture.AllAsTheoryData), MemberType = typeof(FactBaseFixture))]
    public async Task Replaying_a_fixture_spends_no_tokens(string fixture)
    {
        FactBase factBase = FactBaseFixture.Load(fixture);
        RunMetrics metrics = new();

        ReleaseOutcome outcome = await BuildPipeline(factBase, new EchoingChangelogModel(), metrics)
            .RunAsync(Request(factBase), PipelineMode.Generate);

        // No live model, so nothing is billed - the suite can run as often as needed.
        Assert.Equal(0, outcome.Metrics.TotalTokens.TotalTokens);
    }

    [Theory]
    [MemberData(nameof(FactBaseFixture.AllAsTheoryData), MemberType = typeof(FactBaseFixture))]
    public async Task Text_that_only_repeats_the_facts_is_never_flagged(string fixture)
    {
        FactBase factBase = FactBaseFixture.Load(fixture);

        ReleaseOutcome outcome = await BuildPipeline(factBase, new EchoingChangelogModel(), new RunMetrics())
            .RunAsync(Request(factBase), PipelineMode.Generate);

        // The model echoes the facts, so a flag here would be a false positive.
        Assert.All(outcome.Renderings, rendering => Assert.Empty(rendering.Flags));
    }

    [Fact]
    public async Task An_invented_fact_is_flagged_when_replaying_a_fixture()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.Typical);

        ReleaseOutcome outcome = await BuildPipeline(factBase, new InventingChangelogModel(), new RunMetrics())
            .RunAsync(Request(factBase), PipelineMode.Preview);

        // Proves the fixtures exercise the checks for real, not just clean input.
        Assert.All(outcome.Renderings, rendering => Assert.Contains(
            rendering.Flags, flag => flag.Contains("9,001") || flag.Contains("QuantumScheduler")));
    }

    [Fact]
    public async Task The_thorough_check_toggled_off_never_reaches_the_model()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.Typical);
        RunMetrics metrics = new();

        // The renderer needs a working model; only the thorough check must stay away from it.
        ReleasePipeline pipeline = new(
            new StubCommitReader(),
            new StubPullRequestReader(),
            new FixtureFactBaseBuilder(factBase),
            new ReleaseRenderer(new ReleaseChangelogGenerator(new EchoingChangelogModel(), new ChangelogFormatter())),
            new RuleBasedFaithfulnessChecker(),
            new ThoroughFaithfulnessChecker(new UnreachableChangelogModel(), new ThoroughFaithfulnessOptions(false)),
            new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
            new SpyJsonWriter(),
            new SpyMarkdownWriter(),
            new SpyReleaseNotesWriter(),
            metrics);

        ReleaseOutcome outcome = await pipeline.RunAsync(Request(factBase), PipelineMode.Preview);

        Assert.All(outcome.Renderings, rendering => Assert.True(rendering.Success));
        Assert.Equal(3, outcome.Metrics.Thorough.Runs);
        Assert.Equal(0, outcome.Metrics.UsageOf(LlmOperation.FaithfulnessCheck).Calls);
    }

    [Fact]
    public async Task The_customer_rendering_of_an_internal_only_release_carries_no_changes()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.InternalOnly);
        EchoingChangelogModel model = new();

        ReleaseOutcome outcome = await BuildPipeline(factBase, model, new RunMetrics())
            .RunAsync(Request(factBase), PipelineMode.Preview);

        AudienceOutcome customer = outcome.Renderings.Single(r => r.Audience == Audience.Customer);
        Assert.True(customer.Success);
        Assert.Empty(customer.Text!);
    }

    [Fact]
    public async Task Breaking_changes_lead_the_technical_rendering()
    {
        FactBase factBase = FactBaseFixture.Load(FactBaseFixture.Breaking);

        ReleaseOutcome outcome = await BuildPipeline(factBase, new EchoingChangelogModel(), new RunMetrics())
            .RunAsync(Request(factBase), PipelineMode.Preview);

        string technical = outcome.Renderings.Single(r => r.Audience == Audience.Technical).Text!;
        Assert.Contains("(breaking)", technical.Split('\n')[0]);
    }
}

/// <summary>The fixtures themselves must stay valid and canonical, or they prove nothing.</summary>
public sealed class FactBaseFixtureTests
{
    [Theory]
    [MemberData(nameof(FactBaseFixture.AllAsTheoryData), MemberType = typeof(FactBaseFixture))]
    public void Every_fixture_loads_and_carries_its_tag(string fixture)
    {
        FactBase factBase = FactBaseFixture.Load(fixture);

        Assert.False(string.IsNullOrWhiteSpace(factBase.Tag));
    }

    [Theory]
    [MemberData(nameof(FactBaseFixture.AllAsTheoryData), MemberType = typeof(FactBaseFixture))]
    public void Every_fixture_is_exactly_what_a_real_run_would_write(string fixture)
    {
        FactBase factBase = FactBaseFixture.Load(fixture);

        string reserialized = ChangelogJsonSerializer.Serialize(factBase);

        // A fixture is a frozen changelog.json. If the writer's output ever drifts from
        // these files, the fixtures stop representing real runs - so they must match.
        Assert.Equal(
            FactBaseFixture.ReadText(fixture).ReplaceLineEndings().TrimEnd(),
            reserialized.ReplaceLineEndings().TrimEnd());
    }

    [Fact]
    public void A_missing_fixture_says_so_clearly()
    {
        FileNotFoundException error = Assert.Throws<FileNotFoundException>(
            () => FactBaseFixture.Load("no-such-release"));

        Assert.Contains("no-such-release", error.Message);
    }

    [Fact]
    public void The_fixtures_cover_the_cases_the_pipeline_treats_differently()
    {
        FactBase typical = FactBaseFixture.Load(FactBaseFixture.Typical);
        FactBase breaking = FactBaseFixture.Load(FactBaseFixture.Breaking);
        FactBase commitsOnly = FactBaseFixture.Load(FactBaseFixture.CommitsOnly);
        FactBase internalOnly = FactBaseFixture.Load(FactBaseFixture.InternalOnly);

        Assert.Contains(typical.Changes, change => change.LinkedIssues.Count > 0);
        Assert.Contains(breaking.Changes, change => change.IsBreaking);
        Assert.All(commitsOnly.Changes, change => Assert.Null(change.Number));
        Assert.All(commitsOnly.Changes, change => Assert.Null(change.Url));
        Assert.DoesNotContain(internalOnly.Changes, change => change.IsUserVisible);
        Assert.Empty(FactBaseFixture.Load(FactBaseFixture.Empty).Changes);
    }
}
