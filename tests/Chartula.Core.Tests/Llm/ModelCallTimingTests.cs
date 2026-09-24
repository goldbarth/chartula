using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Tests.Llm;

/// <summary>
/// #128: a slow run must be explainable from the run itself: which call took the time,
/// whether it failed, and whether it was sent more than once.
/// </summary>
public sealed class ModelCallTimingTests
{
    private const string NoEntries = """{"entries":[]}""";

    private static RephraseRequest Request() => new(new GroundedFacts(["Adds search"]), Audience.Technical);

    [Fact]
    public async Task A_call_that_answers_late_is_recorded_with_its_duration()
    {
        RunMetrics metrics = new();
        ChatModel model = new(new LateChatClient(TimeSpan.FromMilliseconds(120), NoEntries), new ChangelogPromptBuilder(), metrics: metrics);

        await model.RephraseAsync(Request());

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(1, usage.TotalCalls);
        Assert.True(usage.Duration >= TimeSpan.FromMilliseconds(100), $"recorded {usage.Duration}");
        Assert.Equal(usage.Duration, usage.LongestCall);
    }

    // A client without the counting handler reports no requests. That must not look
    // like a call that succeeded on the first try.
    [Fact]
    public async Task Without_a_counting_transport_retries_are_not_observed()
    {
        RunMetrics metrics = new();
        ChatModel model = new(new StubChatClient(NoEntries), new ChangelogPromptBuilder(), metrics: metrics);

        await model.RephraseAsync(Request());

        Assert.Null(metrics.Snapshot().UsageOf(LlmOperation.Rephrase).Retries);
    }

    // Simulates the transport handler, which records every request it sends.
    [Fact]
    public async Task Requests_the_transport_reports_during_a_call_become_its_retries()
    {
        RunMetrics metrics = new();
        ChatModel model = new(new RetriedChatClient(requests: 3, NoEntries), new ChangelogPromptBuilder(), metrics: metrics);

        await model.RephraseAsync(Request());

        Assert.Equal(2, metrics.Snapshot().UsageOf(LlmOperation.Rephrase).Retries);
    }

    [Fact]
    public async Task A_call_that_fails_is_recorded_as_failed_with_its_time_and_rethrown()
    {
        RunMetrics metrics = new();
        ChatModel model = new(new FailingChatClient(TimeSpan.FromMilliseconds(50)), new ChangelogPromptBuilder(), metrics: metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => model.RephraseAsync(Request()));

        LlmUsage usage = metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
        Assert.Equal(0, usage.TotalCalls);
        Assert.Equal(1, usage.FailedCalls);
        Assert.Equal(0, usage.CallsWithoutUsage);
        Assert.True(usage.Duration >= TimeSpan.FromMilliseconds(40), $"recorded {usage.Duration}");
    }

    // Outside a model call, a request is not counted for any call.
    [Fact]
    public void A_request_outside_a_model_call_is_ignored()
    {
        ModelCallAttempts.RecordRequest();

        using ModelCallAttempts attempts = ModelCallAttempts.Begin();
        Assert.Null(attempts.Observed);
    }

    private sealed class LateChatClient(TimeSpan delay, string text) : DelegatingChatClient(new StubChatClient(text))
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    private sealed class RetriedChatClient(int requests, string text) : DelegatingChatClient(new StubChatClient(text))
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < requests; i++)
            {
                await Task.Yield();
                ModelCallAttempts.RecordRequest();
            }

            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    private sealed class FailingChatClient(TimeSpan delay) : DelegatingChatClient(new StubChatClient(NoEntries))
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            throw new HttpRequestException("overloaded");
        }
    }
}
