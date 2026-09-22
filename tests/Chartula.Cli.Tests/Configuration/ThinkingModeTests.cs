using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using Anthropic;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace Chartula.Cli.Tests.Configuration;

/// <summary>
/// Thinking is billed as output tokens and models disagree about their own default,
/// so it has to be something a user can state rather than discover on an invoice -
/// and state once, in words that mean the same on every provider (#87).
/// </summary>
public sealed class ThinkingModeTests
{
    [Theory]
    [InlineData(null, ThinkingMode.ProviderDefault)]
    [InlineData("", ThinkingMode.ProviderDefault)]
    [InlineData("provider-default", ThinkingMode.ProviderDefault)]
    [InlineData("default", ThinkingMode.ProviderDefault)]
    [InlineData("disabled", ThinkingMode.Disabled)]
    [InlineData("off", ThinkingMode.Disabled)]
    [InlineData("none", ThinkingMode.Disabled)]
    [InlineData("low", ThinkingMode.Low)]
    [InlineData("Medium", ThinkingMode.Medium)]
    [InlineData("high", ThinkingMode.High)]
    [InlineData("xhigh", ThinkingMode.ExtraHigh)]
    [InlineData("extra-high", ThinkingMode.ExtraHigh)]
    public void Parses_the_configured_mode(string? configured, ThinkingMode expected)
    {
        Assert.Equal(expected, ThinkingModeParser.Parse(configured));
    }

    // adaptive and on were the one way to turn thinking on before the effort levels.
    // Adaptive thinking without an effort is high effort, so existing files keep
    // asking for what they asked for.
    [Theory]
    [InlineData("adaptive")]
    [InlineData("Adaptive")]
    [InlineData("on")]
    public void The_earlier_on_values_read_as_high(string configured)
    {
        Assert.Equal(ThinkingMode.High, ThinkingModeParser.Parse(configured));
    }

    [Fact]
    public void Rejects_an_unknown_mode_by_name()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => ThinkingModeParser.Parse("sometimes"));

        // A typo must not fall through to a default and bill differently than asked.
        Assert.Contains("sometimes", error.Message);
        Assert.Contains("provider-default", error.Message);
    }

    [Fact]
    public void The_llm_section_carries_thinking()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(
                """
                llm:
                  thinking: disabled
                """))
            .Build();

        LlmOptions llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>()!;

        Assert.Equal(ThinkingMode.Disabled, ThinkingModeParser.Parse(llm.Thinking));
    }

    // What actually leaves the machine, through the real adapters with only the
    // network stubbed. The mapping lives in the provider packages, so a package update
    // that changed it would otherwise show up only on the invoice.
    [Theory]
    [InlineData("provider-default", null, null, null)]
    [InlineData("disabled", """{"type":"disabled"}""", null, "none")]
    [InlineData("low", """{"type":"adaptive"}""", "low", "low")]
    [InlineData("medium", """{"type":"adaptive"}""", "medium", "medium")]
    [InlineData("high", """{"type":"adaptive"}""", "high", "high")]
    [InlineData("xhigh", """{"type":"adaptive"}""", "xhigh", "xhigh")]
    public async Task One_value_asks_every_provider_for_the_same_thing(
        string thinking, string? anthropicThinking, string? anthropicEffort, string? openAiEffort)
    {
        ChatOptions options = new()
        {
            MaxOutputTokens = 2_000,
            Reasoning = Services("anthropic", "claude-sonnet-5", thinking).Reasoning,
        };

        CapturingHandler anthropic = new(AnthropicReply);
        IChatClient anthropicClient = new AnthropicClient { ApiKey = "k", HttpClient = new HttpClient(anthropic) }
            .AsIChatClient("claude-sonnet-5");
        await anthropicClient.GetResponseAsync("hi", options);

        CapturingHandler openAi = new(OpenAiReply);
        IChatClient openAiClient = new OpenAIClient(
                new ApiKeyCredential("k"),
                new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(openAi)) })
            .GetChatClient("gpt-test").AsIChatClient();
        await openAiClient.GetResponseAsync("hi", options);

        Assert.Equal(anthropicThinking, anthropic.Field("thinking"));
        Assert.Equal(anthropicEffort, anthropic.Field("output_config", "effort"));
        Assert.Equal(openAiEffort, openAi.Field("reasoning_effort"));

        // The request keeps its model and ceiling: nothing replaces the ordinary path.
        Assert.Equal("claude-sonnet-5", anthropic.Field("model"));
        Assert.Equal("2000", anthropic.Field("max_tokens"));
    }

    // The API rejects these combinations on the first request. Refused here instead,
    // so the run fails before it fetches anything rather than after.
    [Theory]
    [InlineData(ThinkingMode.High, "claude-haiku-4-5")]
    [InlineData(ThinkingMode.Low, "claude-haiku-4-5-20251001")]
    [InlineData(ThinkingMode.High, "claude-opus-4-20250514")]
    [InlineData(ThinkingMode.Medium, "claude-3-5-haiku-20241022")]
    [InlineData(ThinkingMode.High, "us.anthropic.claude-sonnet-4-5-20250929-v1:0")]
    [InlineData(ThinkingMode.ExtraHigh, "claude-opus-4-6")]
    [InlineData(ThinkingMode.ExtraHigh, "claude-sonnet-4-6")]
    [InlineData(ThinkingMode.Disabled, "claude-fable-5")]
    [InlineData(ThinkingMode.Disabled, "claude-fable-5-1")]
    public void A_mode_the_claude_model_rejects_fails_at_config_load(ThinkingMode mode, string model)
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => ClaudeThinkingSupport.EnsureModelAccepts(mode, model));

        Assert.Contains("llm.thinking", error.Message);
        Assert.Contains(model, error.Message);
    }

    // An id this cannot read is left to the API: refusing it would block gateway
    // aliases and models released after this list.
    [Theory]
    [InlineData(ThinkingMode.High, "claude-opus-4-6")]
    [InlineData(ThinkingMode.ExtraHigh, "claude-opus-4-8")]
    [InlineData(ThinkingMode.Low, "claude-sonnet-5")]
    [InlineData(ThinkingMode.High, "claude-fable-5-1")]
    [InlineData(ThinkingMode.Disabled, "claude-haiku-4-5")]
    [InlineData(ThinkingMode.Disabled, "claude-opus-5")]
    [InlineData(ThinkingMode.ExtraHigh, "my-gateway-alias")]
    [InlineData(ThinkingMode.ProviderDefault, "claude-haiku-4-5")]
    [InlineData(ThinkingMode.ProviderDefault, "claude-fable-5")]
    public void A_mode_the_model_accepts_or_an_unknown_model_passes(ThinkingMode mode, string model)
    {
        ClaudeThinkingSupport.EnsureModelAccepts(mode, model);
    }

    private static Core.Llm.ChatModelOptions Services(string provider, string model, string thinking)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(
                $"""
                 llm:
                   provider: {provider}
                   model: {model}
                   thinking: {thinking}
                 """))
            .AddInMemoryCollection([new("ANTHROPIC_API_KEY", "k")])
            .Build();

        return new ServiceCollection().AddChartulaLlm(configuration).BuildServiceProvider()
            .GetRequiredService<Core.Llm.ChatModelOptions>();
    }

    private const string AnthropicReply =
        """{"id":"m","type":"message","role":"assistant","model":"x","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":1,"output_tokens":1}}""";

    private const string OpenAiReply =
        """{"id":"c","object":"chat.completion","created":1,"model":"x","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";

    /// <summary>Records the first request body and answers with a canned reply.</summary>
    private sealed class CapturingHandler(string reply) : HttpMessageHandler
    {
        private string? _body;

        /// <summary>A field of the request body as raw JSON (strings unquoted), or null when absent.</summary>
        public string? Field(params string[] path)
        {
            using JsonDocument document = JsonDocument.Parse(_body!);
            JsonElement element = document.RootElement;
            foreach (string name in path)
            {
                if (!element.TryGetProperty(name, out element))
                {
                    return null;
                }
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _body ??= await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
