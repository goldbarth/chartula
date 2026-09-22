using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using Anthropic;
using Chartula.Cli.Composition;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// #128: both provider SDKs retry on their own. The handler under their clients is
/// what turns those retries into a number the run can report - checked here against
/// the real SDKs, with only the network stubbed.
/// </summary>
public sealed class ModelRequestCountingTests
{
    private const string AnthropicReply =
        """{"id":"m","type":"message","role":"assistant","model":"x","content":[{"type":"text","text":"{\"entries\":[]}"}],"stop_reason":"end_turn","usage":{"input_tokens":5000,"output_tokens":2}}""";

    private const string OpenAiReply =
        """{"id":"c","object":"chat.completion","created":1,"model":"x","choices":[{"index":0,"message":{"role":"assistant","content":"{\"entries\":[]}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5000,"completion_tokens":2,"total_tokens":5002}}""";

    [Fact]
    public async Task An_anthropic_call_that_is_overloaded_once_reports_one_retry()
    {
        HttpClient http = ModelRequestCountingHandler.CreateClient(new FailsOnceHandler((HttpStatusCode)529, AnthropicReply));
        IChatClient chat = new AnthropicClient { ApiKey = "k", HttpClient = http }.AsIChatClient("claude-sonnet-5");

        LlmUsage usage = await RephraseAsync(chat);

        Assert.Equal(1, usage.TotalCalls);
        Assert.Equal(1, usage.Retries);
    }

    [Fact]
    public async Task An_openai_call_that_is_unavailable_once_reports_one_retry()
    {
        HttpClient http = ModelRequestCountingHandler.CreateClient(new FailsOnceHandler(HttpStatusCode.ServiceUnavailable, OpenAiReply));
        IChatClient chat = new OpenAIClient(
                new ApiKeyCredential("k"),
                new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(http) })
            .GetChatClient("gpt-test").AsIChatClient();

        LlmUsage usage = await RephraseAsync(chat);

        Assert.Equal(1, usage.TotalCalls);
        Assert.Equal(1, usage.Retries);
    }

    [Fact]
    public async Task A_call_answered_first_time_reports_no_retries_rather_than_none_observed()
    {
        HttpClient http = ModelRequestCountingHandler.CreateClient(new FailsOnceHandler(null, AnthropicReply));
        IChatClient chat = new AnthropicClient { ApiKey = "k", HttpClient = http }.AsIChatClient("claude-sonnet-5");

        Assert.Equal(0, (await RephraseAsync(chat)).Retries);
    }

    // The SDKs enforce their own timeout; the HttpClient default of 100 seconds
    // would cut a long thinking call off before it.
    [Fact]
    public void The_client_leaves_timeouts_to_the_sdk()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, ModelRequestCountingHandler.CreateClient().Timeout);
    }

    private static async Task<LlmUsage> RephraseAsync(IChatClient chat)
    {
        RunMetrics metrics = new();
        ChatModel model = new(chat, new ChangelogPromptBuilder(), metrics: metrics);
        await model.RephraseAsync(new RephraseRequest(new GroundedFacts(["Adds search"]), Audience.Technical));
        return metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
    }

    /// <summary>Answers the first request with <paramref name="firstStatus"/> (if any), then succeeds.</summary>
    private sealed class FailsOnceHandler(HttpStatusCode? firstStatus, string reply) : HttpMessageHandler
    {
        private int _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bool fail = firstStatus is not null && Interlocked.Increment(ref _requests) == 1;
            return Task.FromResult(fail
                ? new HttpResponseMessage(firstStatus!.Value)
                {
                    Content = new StringContent("""{"type":"error","error":{"type":"overloaded_error","message":"busy"}}"""),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(reply, System.Text.Encoding.UTF8, "application/json"),
                });
        }
    }
}
