using Anthropic;
using Chartula.Cli.Configuration;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the LLM seam. This is the only place that knows which
/// concrete provider backs <see cref="IChangelogModel"/>; swapping providers is
/// a change here and nowhere else.
/// </summary>
internal static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // The provider is read first because it decides what the other keys default
        // to. Reading it later would mean defaulting the model before knowing whose
        // model it is.
        LlmProvider provider = LlmProviderParser.Parse(configuration[$"{LlmOptions.SectionName}:Provider"]);
        LlmOptions options = ReadOptions(configuration, provider);

        ThoroughCheckModel check = ThoroughCheckModel.Resolve(options, ThoroughCheckModel.Read(configuration));

        services.AddSingleton(options);
        services.AddSingleton(new ChatModelOptions
        {
            MaxOutputTokens = options.MaxOutputTokens,
            Reasoning = Reasoning(provider, options.Model, ThinkingModeParser.Parse(options.Thinking)),
            // Sent only when it differs: the client already asks the rendering model.
            CheckModelId = check.Model == options.Model ? null : check.Model,
            CheckReasoning = Reasoning(provider, check.Model, check.Thinking, check.ThinkingKey, check.ModelKey),
        });

        // Last among the refusals: a setting that is wrong in chartula.yaml is named
        // before the key, which may be missing only because the file was not fixed yet.
        RequireApiKey(provider, options, configuration);
        services.AddSingleton(sp => CreateChatClient(provider, options, configuration));
        services.AddSingleton<IChangelogPromptBuilder, ChangelogPromptBuilder>();
        services.AddSingleton<IChangelogModel, ChatModel>();
        return services;
    }

    /// <summary>The options a run's configuration resolves to, provider defaults applied.</summary>
    public static LlmOptions ReadOptions(IConfiguration configuration)
        => ReadOptions(configuration, LlmProviderParser.Parse(configuration[$"{LlmOptions.SectionName}:Provider"]));

    private static LlmOptions ReadOptions(IConfiguration configuration, LlmProvider provider)
    {
        LlmProviderDefaults defaults = LlmProviderDefaults.For(provider);

        string? model = configuration[$"{LlmOptions.SectionName}:Model"] ?? defaults.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                $"llm.model is required when llm.provider is '{LlmProviderParser.ToConfigurationValue(provider)}'. " +
                "There is no default, because the models an endpoint serves are its own - " +
                "ask it, for example with 'ollama list' or GET /v1/models.");
        }

        string apiKeyVariable = configuration[$"{LlmOptions.SectionName}:ApiKeyEnvironmentVariable"]
                                ?? defaults.ApiKeyEnvironmentVariable;
        string? baseUrl = ReadBaseUrl(configuration) ?? defaults.BaseUrl;

        // Here rather than where the client is built: the options are read before the
        // run starts, and a key that went out cannot be called back.
        ProviderHost.RequireOwnedBy(
            provider,
            providerConfigured: !string.IsNullOrWhiteSpace(configuration[$"{LlmOptions.SectionName}:Provider"]),
            baseUrl,
            apiKeyVariable);

        return new LlmOptions
        {
            Provider = LlmProviderParser.ToConfigurationValue(provider),
            Model = model,
            ApiKeyEnvironmentVariable = apiKeyVariable,
            BaseUrl = baseUrl,
            MaxOutputTokens = ReadMaxOutputTokens(configuration),
            Thinking = configuration[$"{LlmOptions.SectionName}:Thinking"],
        };
    }

    // Checked for both providers: a proxy in front of the Anthropic API receives the
    // key just the same.
    private static string? ReadBaseUrl(IConfiguration configuration)
    {
        string? value = configuration[$"{LlmOptions.SectionName}:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(value))
        {
            EndpointUrl.Require(LlmOptions.BaseUrlVariable, value);
        }

        return value;
    }

    /// <summary>
    /// Refuses an Anthropic run without a key before any work starts. Without this the
    /// run reads the history and spends a GitHub request per pull request, and only
    /// then fails every audience on the provider's raw 401, which never names the
    /// variable. An OpenAI-compatible endpoint is left alone: a local server needs no
    /// key, and whether a hosted one does is the endpoint's to say.
    /// </summary>
    private static void RequireApiKey(LlmProvider provider, LlmOptions options, IConfiguration configuration)
    {
        if (provider != LlmProvider.Anthropic
            || !string.IsNullOrWhiteSpace(configuration[options.ApiKeyEnvironmentVariable]))
        {
            return;
        }

        string variable = options.ApiKeyEnvironmentVariable;
        throw new InvalidOperationException(
            $"No Anthropic API key found in {variable}. " +
            $"Set one with: export {variable}=<your key> (create one at https://console.anthropic.com/settings/keys). " +
            "For an endpoint that needs no key, set llm.provider to openai-compatible.");
    }

    // An unparsable or non-positive value would otherwise fall through to the
    // provider default and truncate silently, so reject it loudly instead.
    private static int ReadMaxOutputTokens(IConfiguration configuration)
    {
        string? raw = configuration[$"{LlmOptions.SectionName}:MaxOutputTokens"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new LlmOptions().MaxOutputTokens;
        }

        if (!int.TryParse(raw, out int value) || value <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid llm.maxOutputTokens '{raw}'. Expected a positive whole number.");
        }

        return value;
    }

    /// <summary>
    /// The configured thinking mode as the provider-neutral request field. Both
    /// adapters translate it themselves - Anthropic to thinking plus effort, OpenAI to
    /// <c>reasoning_effort</c> - so the same value means the same thing on either.
    /// </summary>
    private static ReasoningOptions? Reasoning(
        LlmProvider provider,
        string model,
        ThinkingMode mode,
        string thinkingKey = "llm.thinking",
        string modelKey = "llm.model")
    {
        if (provider == LlmProvider.Anthropic)
        {
            ClaudeThinkingSupport.EnsureModelAccepts(mode, model, thinkingKey, modelKey);
        }

        ReasoningEffort? effort = mode switch
        {
            ThinkingMode.Disabled => ReasoningEffort.None,
            ThinkingMode.Low => ReasoningEffort.Low,
            ThinkingMode.Medium => ReasoningEffort.Medium,
            ThinkingMode.High => ReasoningEffort.High,
            ThinkingMode.ExtraHigh => ReasoningEffort.ExtraHigh,
            _ => null,
        };

        // Provider default sends nothing at all: the absence is what leaves each model
        // on its own behavior.
        return effort is null ? null : new ReasoningOptions { Effort = effort };
    }

    private static IChatClient CreateChatClient(
        LlmProvider provider,
        LlmOptions options,
        IConfiguration configuration)
    {
        // Read the key by name; never hardcode it. An Anthropic run without one was
        // refused at registration; an OpenAI-compatible endpoint may need none.
        string? apiKey = configuration[options.ApiKeyEnvironmentVariable];

        return provider switch
        {
            LlmProvider.Anthropic => CreateAnthropicClient(options, apiKey),
            LlmProvider.OpenAiCompatible => OpenAiCompatibleChatClient.Create(options, apiKey),
            _ => throw new NotSupportedException(
                $"LLM provider '{options.Provider}' is not supported yet."),
        };
    }

    private static IChatClient CreateAnthropicClient(LlmOptions options, string? apiKey)
    {
        // Two initializers rather than an assignment: BaseUrl is init-only, and its
        // own default is a real URL, so passing null through would blank it. Left
        // alone unless configured - a base URL is only set here for a proxy or a
        // gateway in front of the API.
        HttpClient http = ModelRequestCountingHandler.CreateClient();
        AnthropicClient client = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? new AnthropicClient { ApiKey = apiKey, HttpClient = http }
            : new AnthropicClient { ApiKey = apiKey, BaseUrl = options.BaseUrl, HttpClient = http };

        return client.AsIChatClient(options.Model);
    }
}
