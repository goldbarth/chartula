using System.Collections;
using System.Net;
using System.Text;
using Chartula.Cli.Composition;
using Chartula.Cli.Configuration;
using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace Chartula.Cli.Tests.Composition;

/// <summary>
/// #234: a failed model call reported only <c>Status Code: NotFound</c>. Checked here
/// through the client a run's configuration builds - the real SDKs, with only the
/// network stubbed - so what each SDK puts in its exception cannot leak through.
/// </summary>
public sealed class ModelCallFailureTests
{
    private const string AnthropicNotFound =
        """{"type":"error","error":{"type":"not_found_error","message":"model: claude-nonexistent"}}""";

    private const string OpenAiNotFound =
        """{"error":{"message":"The model `gpt-luna` does not exist.","type":"invalid_request_error","code":"model_not_found"}}""";

    private const string Unauthorized = """{"error":{"message":"invalid key"}}""";

    // Each provider's base URL in its own convention: Anthropic's SDK adds /v1 itself.
    private static string BaseUrlOf(string provider)
        => provider == "anthropic" ? "http://localhost:8799" : "http://localhost:8799/v1";

    public static TheoryData<string, string> Providers => new()
    {
        { "anthropic", "http://localhost:8799/v1/messages" },
        { "openai-compatible", "http://localhost:8799/v1/chat/completions" },
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_404_names_the_provider_the_endpoint_the_model_the_status_and_the_likely_causes(
        string provider, string requested)
    {
        string body = provider == "anthropic" ? AnthropicNotFound : OpenAiNotFound;

        string message = await FailAsync(provider, HttpStatusCode.NotFound, body);

        Assert.StartsWith($"{provider} at {requested} answered 404 Not Found for model 'some-model'.", message);
        Assert.Contains("the endpoint does not serve that model id (check llm.model)", message);
        Assert.Contains("Chartula__Llm__BaseUrl is not the address of this provider's API", message);
        Assert.Contains("The endpoint said: " + (provider == "anthropic" ? "model: claude-nonexistent" : "The model `gpt-luna` does not exist."), message);
        Assert.DoesNotContain("Status Code", message);
    }

    // Without a base URL the endpoint is Anthropic's own, so it cannot be the wrong one.
    [Fact]
    public async Task A_404_from_the_default_endpoint_names_only_the_model()
    {
        string message = await FailAsync("anthropic", HttpStatusCode.NotFound, AnthropicNotFound, baseUrl: null);

        Assert.StartsWith("anthropic at https://api.anthropic.com/v1/messages answered 404 Not Found for model 'some-model'.", message);
        Assert.Contains("The endpoint does not serve that model id - check llm.model.", message);
        Assert.DoesNotContain("Chartula__Llm__BaseUrl", message);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_401_names_the_key_variable(string provider, string requested)
    {
        string variable = provider == "anthropic" ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY";

        string message = await FailAsync(provider, HttpStatusCode.Unauthorized, Unauthorized);

        Assert.StartsWith($"{provider} at {requested} answered 401 Unauthorized for model 'some-model'.", message);
        Assert.Contains($"The endpoint rejected the key in {variable}", message);
        Assert.Contains("The endpoint said: invalid key", message);
    }

    // An OpenAI-compatible run starts without a key, so a 401 may mean there is none.
    [Fact]
    public async Task A_401_without_a_key_says_the_variable_is_not_set()
    {
        string message = await FailAsync("openai-compatible", HttpStatusCode.Unauthorized, Unauthorized, withKey: false);

        Assert.Contains("The endpoint needs a key, and OPENAI_API_KEY is not set.", message);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_403_names_the_key_variable(string provider, string requested)
    {
        string variable = provider == "anthropic" ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY";

        string message = await FailAsync(provider, HttpStatusCode.Forbidden, Unauthorized);

        Assert.StartsWith($"{provider} at {requested} answered 403 Forbidden", message);
        Assert.Contains($"The key in {variable} is not allowed to make this request", message);
    }

    // The thorough check may ask a model of its own; the failure names that one, and
    // the key it is set under.
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_failed_check_names_the_check_s_model_and_its_key(string provider, string requested)
    {
        IChatClient chat = Client(provider, new Answering(HttpStatusCode.NotFound, ""), faithfulnessModel: "check-model");
        ChatModel model = new(chat, new ChangelogPromptBuilder(), new ChatModelOptions { CheckModelId = "check-model" });

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("- Added search", new GroundedFacts(["Feature: Adds search"]))));

        Assert.StartsWith($"{provider} at {requested} answered 404 Not Found for model 'check-model'.", error.Message);
        Assert.Contains("(check faithfulness.model)", error.Message);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_error_without_a_body_leaves_out_what_the_endpoint_said(string provider, string requested)
    {
        string message = await FailAsync(provider, HttpStatusCode.NotFound, "");

        Assert.StartsWith($"{provider} at {requested} answered 404 Not Found", message);
        Assert.DoesNotContain("The endpoint said", message);
    }

    // No response at all: the endpoint is the configured one, and the transport's own
    // words say why - not the SDK's wrapper around them.
    [Theory]
    [InlineData("anthropic")]
    [InlineData("openai-compatible")]
    public async Task An_endpoint_that_cannot_be_reached_is_named_with_the_transport_s_reason(string provider)
    {
        string message = await FailAsync(provider, new Unreachable());

        Assert.Equal(
            $"{provider} at {BaseUrlOf(provider)} could not be reached for model 'some-model': Connection refused (localhost:8799)",
            message);
    }

    private static Task<string> FailAsync(
        string provider, HttpStatusCode status, string body, string? baseUrl = "", bool withKey = true)
        => FailAsync(provider, new Answering(status, body), baseUrl, withKey);

    private static async Task<string> FailAsync(
        string provider, HttpMessageHandler transport, string? baseUrl = "", bool withKey = true)
    {
        ChatModel model = new(Client(provider, transport, baseUrl, withKey), new ChangelogPromptBuilder());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => model.RephraseAsync(
            new RephraseRequest(new GroundedFacts(["Adds search"]), Audience.Technical)));
        return error.Message;
    }

    private static IChatClient Client(
        string provider,
        HttpMessageHandler transport,
        string? baseUrl = "",
        bool withKey = true,
        string? faithfulnessModel = null)
    {
        Hashtable environment = new()
        {
            ["Chartula__Llm__Provider"] = provider,
            ["Chartula__Llm__Model"] = "some-model",
        };
        // Empty stands for the provider's local stub, null for no base URL at all.
        if (baseUrl is not null)
        {
            environment["Chartula__Llm__BaseUrl"] = baseUrl.Length > 0 ? baseUrl : BaseUrlOf(provider);
        }

        if (faithfulnessModel is not null)
        {
            environment["Chartula__Faithfulness__Model"] = faithfulnessModel;
        }

        if (withKey)
        {
            environment[provider == "anthropic" ? "ANTHROPIC_API_KEY" : "OPENAI_API_KEY"] = "test-key";
        }

        IConfiguration configuration = ChartulaConfiguration.Build(new ConfigurationBuilder(), environment);
        return LlmServiceCollectionExtensions.CreateChatClient(configuration, transport);
    }

    /// <summary>Answers every request with one status and body, as an endpoint that refuses does.</summary>
    private sealed class Answering(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    /// <summary>Fails every request the way a refused connection does.</summary>
    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused (localhost:8799)");
    }
}
