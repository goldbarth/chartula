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
using Microsoft.Extensions.AI;

namespace Chartula.Core.Tests.Llm;

/// <summary>
/// The run as #85 hit it: an endpoint whose context window is smaller than the prompt.
/// It silently cuts the prompt and reports its own limit as the input token count.
/// Every component except the chat client is the production type.
/// </summary>
public sealed class TruncatedEndpointTests
{
    [Fact]
    public async Task A_run_against_a_truncating_endpoint_renders_nothing_and_says_why()
    {
        // A release large enough that its prompt cannot fit 4,096 tokens.
        FactBase typical = FactBaseFixture.Load(FactBaseFixture.Typical);
        FactBase large = typical with
        {
            Changes = [.. Enumerable.Range(1, 600).Select(i => typical.Changes[0] with { Title = $"feat: change {i} with a title long enough to count" })],
        };

        // Ollama's answer to a 12,400-token prompt with a 4,096-token window.
        StubChatClient chat = new(
            """{"entries":[{"id":1,"text":"Change one."}]}""",
            new UsageDetails { InputTokenCount = 4_096, OutputTokenCount = 20 });
        ChatModel model = new(chat, new ChangelogPromptBuilder());
        SpyJsonWriter json = new();

        ReleaseOutcome outcome = await new ReleasePipeline(
                new StubCommitReader(),
                new StubPullRequestReader(),
                new FixtureFactBaseBuilder(large),
                new ReleaseRenderer(new ReleaseChangelogGenerator(model, new ChangelogFormatter())),
                new RuleBasedFaithfulnessChecker(),
                new ThoroughFaithfulnessChecker(model, new ThoroughFaithfulnessOptions(true)),
                new ReviewCoordinator(new AutoApproveReviewer(), new ReviewOptions(Enabled: false)),
                json,
                new SpyMarkdownWriter(),
                new SpyCustomerPageWriter(),
                new SpyReleaseNotesWriter())
            .RunAsync(new ReleaseRequest(large.Tag, new RepositoryCoordinates("owner", "repo")), PipelineMode.Generate);

        Assert.All(outcome.Renderings, rendering =>
        {
            Assert.False(rendering.Success);
            Assert.Contains("context window is too small", rendering.Error);
        });
        Assert.Equal(0, json.Calls);
    }
}
