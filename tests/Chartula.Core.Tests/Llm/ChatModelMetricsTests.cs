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
    private const string NoEntries = """{"entries":[]}""";

    private static readonly GroundedFacts Facts = new(["[1] Fixed a bug where expired tokens were accepted"]);

    [Fact]
    public async Task Rephrasing_records_its_call_and_tokens_under_the_rephrase_operation()
    {
        // A count the prompt can have: below its characters / 8 would read as truncation.
        StubChatClient chat = new(NoEntries, new UsageDetails { InputTokenCount = 1_200, OutputTokenCount = 34 });
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics: metrics)
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.TotalCalls);
        Assert.Equal(new TokenUsage(1_200, 34), usage.Tokens);
    }

    [Fact]
    public async Task The_faithfulness_check_records_its_tokens_under_its_own_operation()
    {
        StubChatClient chat = new(
            """{"isFaithful":true,"unsupportedClaims":[]}""",
            new UsageDetails { InputTokenCount = 900, OutputTokenCount = 12 });
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics: metrics)
            .CheckFaithfulnessAsync(new FaithfulnessRequest("Some output.", Facts));

        RunReport report = metrics.Snapshot();
        Assert.Equal(1, report.UsageOf(LlmOperation.FaithfulnessCheck).TotalCalls);
        Assert.Equal(912, report.UsageOf(LlmOperation.FaithfulnessCheck).Tokens.TotalTokens);
        // The check's cost stays separate from rephrasing, or it could not be judged.
        Assert.Equal(LlmUsage.None, report.UsageOf(LlmOperation.Rephrase));
    }

    [Fact]
    public async Task A_provider_that_reports_no_usage_still_records_the_call()
    {
        StubChatClient chat = new(NoEntries);
        RunMetrics metrics = new();

        await new ChatModel(chat, new ChangelogPromptBuilder(), metrics: metrics)
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.TotalCalls);
        Assert.Equal(TokenUsage.None, usage.Tokens);
        // Zero tokens because nothing was reported, not because the call was free.
        Assert.Equal(1, usage.CallsWithoutUsage);
    }

    [Fact]
    public async Task Without_a_metrics_sink_the_model_still_works()
    {
        StubChatClient chat = new(NoEntries);

        RenderedEntries result = await new ChatModel(chat, new ChangelogPromptBuilder())
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer));

        Assert.Empty(result.Entries);
    }

    // A call that does not count was still paid for, so a truncated one is recorded
    // before it fails.
    [Fact]
    public async Task A_truncated_rephrase_call_is_recorded_before_it_fails()
    {
        StubChatClient chat = new(NoEntries, new UsageDetails { InputTokenCount = 10, OutputTokenCount = 3 });
        RunMetrics metrics = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ChatModel(chat, new ChangelogPromptBuilder(), metrics: metrics)
            .RephraseAsync(new RephraseRequest(Facts, Audience.Customer)));

        Assert.Equal(new TokenUsage(10, 3), metrics.Snapshot().UsageOf(LlmOperation.Rephrase).Tokens);
    }
}
