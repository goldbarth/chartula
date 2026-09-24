using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using Anthropic;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// Which part of a call's tokens came from the provider's cache, and which part the
/// model spent reasoning, read through the real adapters.
/// Providers report these differently, so a value a provider does not report must stay
/// unknown, not zero.
/// </summary>
public sealed class UsageDetailTests
{
    [Fact]
    public async Task Anthropic_reports_cache_reads_and_no_separate_reasoning()
    {
        const string reply =
            """{"id":"m","type":"message","role":"assistant","model":"x","content":[{"type":"text","text":"{\"entries\":[]}"}],"stop_reason":"end_turn","usage":{"input_tokens":1000,"cache_read_input_tokens":4000,"output_tokens":300}}""";
        IChatClient chat = new AnthropicClient { ApiKey = "k", HttpClient = new HttpClient(new Fixed(reply)) }
            .AsIChatClient("claude-sonnet-5");

        LlmUsage usage = await RephraseAsync(chat);

        // The adapter counts cache reads as part of the input.
        Assert.Equal(5_000, usage.Tokens.InputTokens);
        Assert.Equal(4_000, usage.CachedInputTokens);
        Assert.Null(usage.ReasoningTokens);
    }

    [Fact]
    public async Task OpenAi_reports_cached_input_and_reasoning()
    {
        const string reply =
            """{"id":"c","object":"chat.completion","created":1,"model":"x","choices":[{"index":0,"message":{"role":"assistant","content":"{\"entries\":[]}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5000,"completion_tokens":300,"total_tokens":5300,"prompt_tokens_details":{"cached_tokens":4000},"completion_tokens_details":{"reasoning_tokens":250}}}""";
        IChatClient chat = new OpenAIClient(
                new ApiKeyCredential("k"),
                new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(new Fixed(reply))) })
            .GetChatClient("gpt-test").AsIChatClient();

        LlmUsage usage = await RephraseAsync(chat);

        Assert.Equal(5_000, usage.Tokens.InputTokens);
        Assert.Equal(4_000, usage.CachedInputTokens);
        Assert.Equal(250, usage.ReasoningTokens);
    }

    private static async Task<LlmUsage> RephraseAsync(IChatClient chat)
    {
        RunMetrics metrics = new();
        ChatModel model = new(chat, new ChangelogPromptBuilder(), metrics: metrics);
        await model.RephraseAsync(new RephraseRequest(new GroundedFacts(["Adds search"]), Audience.Technical));
        return metrics.Snapshot().UsageOf(LlmOperation.Rephrase);
    }

    private sealed class Fixed(string reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
