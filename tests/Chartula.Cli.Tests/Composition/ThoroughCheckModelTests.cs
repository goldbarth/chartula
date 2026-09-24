using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections;
using System.Net;
using System.Text.Json;
using Anthropic;
using Chartula.Cli.Commands;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;
using Chartula.Core.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// The thorough check can use its own model and thinking mode. Rendering writes prose,
/// the check compares claims with facts, and the two jobs need not cost the same.
/// </summary>
public sealed class ThoroughCheckModelTests
{
    private static IConfiguration Configure(string yaml)
        => ChartulaConfiguration.Build(
            new ConfigurationBuilder().AddInMemoryCollection(ChartulaYamlConfiguration.Flatten(yaml)),
            new Hashtable { ["ANTHROPIC_API_KEY"] = "test-key" });

    private static ChatModelOptions Options(string yaml)
        => new ServiceCollection().AddChartulaLlm(Configure(yaml)).BuildServiceProvider()
            .GetRequiredService<ChatModelOptions>();

    [Fact]
    public void Unset_the_check_asks_what_renders()
    {
        ChatModelOptions options = Options("llm:\n  model: claude-sonnet-5\n  thinking: low");

        Assert.Null(options.CheckModelId);
        Assert.Equal(ReasoningEffort.Low, options.CheckReasoning?.Effort);
    }

    [Fact]
    public void The_check_takes_its_own_model_and_thinking()
    {
        ChatModelOptions options = Options(
            """
            llm:
              model: claude-haiku-4-5
              thinking: disabled
            faithfulness:
              model: claude-opus-5
              thinking: high
            """);

        Assert.Equal(ReasoningEffort.None, options.Reasoning?.Effort);
        Assert.Equal("claude-opus-5", options.CheckModelId);
        Assert.Equal(ReasoningEffort.High, options.CheckReasoning?.Effort);
    }

    // A refusal names the setting in effect, not a setting the user did not write.
    [Fact]
    public void A_mode_the_check_model_rejects_is_refused_by_its_own_keys()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Options(
            """
            llm:
              model: claude-sonnet-5
            faithfulness:
              model: claude-haiku-4-5
              thinking: high
            """));

        Assert.Contains("faithfulness.thinking 'high'", error.Message);
        Assert.Contains("faithfulness.model 'claude-haiku-4-5'", error.Message);
    }

    // On the wire, through both real adapters: the check's request carries the check
    // model and effort, the rendering's request does not.
    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai")]
    public async Task Only_the_check_request_carries_the_check_model(string provider)
    {
        ChatModelOptions options = new()
        {
            MaxOutputTokens = 1_000,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None },
            CheckModelId = "check-model",
            CheckReasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
        };
        Capturing handler = new(provider);
        IChatClient chat = provider == "anthropic"
            ? new AnthropicClient { ApiKey = "k", HttpClient = new HttpClient(handler) }.AsIChatClient("render-model")
            : new OpenAIClient(
                    new ApiKeyCredential("k"),
                    new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) })
                .GetChatClient("render-model").AsIChatClient();
        ChatModel model = new(chat, new ChangelogPromptBuilder(), options);
        GroundedFacts facts = new(["Adds search"]);

        await model.RephraseAsync(new RephraseRequest(facts, Audience.Technical));
        await model.CheckFaithfulnessAsync(new FaithfulnessRequest("- Adds search", facts));

        Assert.Equal("render-model", handler.Bodies[0].GetProperty("model").GetString());
        Assert.Equal("check-model", handler.Bodies[1].GetProperty("model").GetString());
        string effortField = provider == "anthropic" ? "thinking" : "reasoning_effort";
        Assert.NotEqual(handler.Bodies[0].GetProperty(effortField).GetRawText(), handler.Bodies[1].GetProperty(effortField).GetRawText());
    }

    [Fact]
    public void The_provenance_records_the_check_model_resolved_and_only_when_the_check_ran()
    {
        LlmOptions llm = new() { Model = "gpt-luna", Thinking = "disabled" };

        RunProvenance inherited = OutputServiceCollectionExtensions.Provenance(llm, new FaithfulnessOptions(), FactBaseDepthParser.Default);
        RunProvenance own = OutputServiceCollectionExtensions.Provenance(
            llm, new FaithfulnessOptions { Model = "gpt-sol", Thinking = "high" }, FactBaseDepthParser.Default);
        RunProvenance off = OutputServiceCollectionExtensions.Provenance(
            llm, new FaithfulnessOptions { Thorough = false, Model = "gpt-sol" }, FactBaseDepthParser.Default);

        Assert.Equal(("gpt-luna", "disabled"), (inherited.CheckModel, inherited.CheckThinking));
        Assert.Equal(("gpt-sol", "high"), (own.CheckModel, own.CheckThinking));
        Assert.Equal((null, null), (off.CheckModel, off.CheckThinking));
    }

    [Fact]
    public void The_run_header_names_the_check_model_only_when_it_differs()
    {
        string same = EndpointNotice.For(Configure("llm:\n  model: claude-sonnet-5"));
        string own = EndpointNotice.For(Configure("llm:\n  model: claude-sonnet-5\nfaithfulness:\n  model: claude-opus-5\n  thinking: disabled"));

        Assert.DoesNotContain("thorough check:", same);
        Assert.Contains("        thorough check: claude-opus-5, thinking disabled", own);
    }

    /// <summary>Records every request body and answers each with a valid reply for its call.</summary>
    private sealed class Capturing(string provider) : HttpMessageHandler
    {
        public List<JsonElement> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone());
            string text = Bodies.Count == 1
                ? """{"entries":[]}"""
                : """{"isFaithful":true,"unsupportedClaims":[]}""";
            string escaped = JsonSerializer.Serialize(text);
            string reply = provider == "anthropic"
                ? $$$"""{"id":"m","type":"message","role":"assistant","model":"x","content":[{"type":"text","text":{{{escaped}}}}],"stop_reason":"end_turn","usage":{"input_tokens":5000,"output_tokens":5}}"""
                : $$$"""{"id":"c","object":"chat.completion","created":1,"model":"x","choices":[{"index":0,"message":{"role":"assistant","content":{{{escaped}}}},"finish_reason":"stop"}],"usage":{"prompt_tokens":5000,"completion_tokens":5,"total_tokens":5005}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
