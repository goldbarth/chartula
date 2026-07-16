using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Tests.Llm;

/// <summary>
/// The chat model is the only place that sees real token usage, so it is what feeds
/// the run metrics.
/// </summary>
public sealed class ChatModelMetricsTests
{
    private static readonly GroundedFacts Facts = new(["Fixed a bug where expired tokens were accepted"]);

    [Fact]
    public async Task Rephrasing_records_its_call_and_tokens_under_the_rephrase_operation()
    {
        StubChatClient chat = new("Text.", new UsageDetails { InputTokenCount = 120, OutputTokenCount = 34 });
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics)
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.Calls);
        Assert.Equal(new TokenUsage(120, 34), usage.Tokens);
    }

    [Fact]
    public async Task The_faithfulness_check_records_its_tokens_under_its_own_operation()
    {
        StubChatClient chat = new(
            """{"isFaithful":true,"unsupportedClaims":[]}""",
            new UsageDetails { InputTokenCount = 900, OutputTokenCount = 12 });
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics)
            .CheckFaithfulnessAsync(new FaithfulnessRequest("Some output.", Facts));

        RunReport report = metrics.Snapshot();
        Assert.Equal(1, report.UsageOf(LlmOperation.FaithfulnessCheck).Calls);
        Assert.Equal(912, report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens.TotalTokens);
        // The check's cost stays separate from rephrasing, or it could not be judged.
        Assert.Equal(LlmUsage.None, report.UsageOf(LlmOperation.Rephrase));
    }

    [Fact]
    public async Task A_provider_that_reports_no_usage_still_records_the_call()
    {
        StubChatClient chat = new("Text.");
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics)
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.Calls);
        Assert.Equal(TokenUsage.None, usage.Tokens);
    }

    [Fact]
    public async Task Without_a_metrics_sink_the_model_still_works()
    {
        StubChatClient chat = new("Text.");

        string result = await new ChatModel(chat, new ChangelogPromptBuilder())
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        Assert.Equal("Text.", result);
    }
}
