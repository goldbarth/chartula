using Anthropic;
using Chartula.Cli.Configuration;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chartula.Cli.Composition;

/// <summary>
/// Composition root for the LLM seam.
/// This is the only place that knows which provider backs <see cref="IChangelogModel"/>,
/// so swapping providers changes this file and nothing else.
/// </summary>
internal static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddChartulaLlm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read the provider first, because it decides the defaults of the other keys,
        // for example the default model.
        LlmProvider provider = LlmProviderParser.Parse(configuration[$"{LlmOptions.SectionName}:Provider"]);
        LlmOptions options = ReadOptions(configuration, provider);

        ThoroughCheckModel check = ThoroughCheckModel.Resolve(options, ThoroughCheckModel.Read(configuration));

        services.AddSingleton(options);
        services.AddSingleton(new ChatModelOptions
        {
            MaxOutputTokens = options.MaxOutputTokens,
            Reasoning = Reasoning(provider, options.Model, ThinkingModeParser.Parse(options.Thinking)),
            // Send the check model only when it differs: the client already uses the rendering model.
            CheckModelId = check.Model == options.Model ? null : check.Model,
            CheckReasoning = Reasoning(provider, check.Model, check.Thinking, check.ThinkingKey, check.ModelKey),
        });

        // Check the key last: report a wrong setting in chartula.yaml first, because the
        // key may be missing only because the file is not fixed yet.
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

        // Check here instead of where the client is built: the options are read before
        // the run starts, and a key that was sent cannot be taken back.
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

    // Validate the base URL for both providers: a proxy in front of the Anthropic API
    // also receives the key.
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
    /// Refuses an Anthropic run without a key before any work starts.
    /// Otherwise the run reads the history, spends a GitHub request per pull request, and
    /// only then fails every audience with the provider's raw 401, which never names the
    /// variable.
    /// An OpenAI-compatible run is not checked: a local server needs no key, and a
    /// hosted endpoint decides for itself whether it needs one.
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

    // Reject an unparsable or non-positive value loudly. Otherwise it would fall back
    // to the provider default and silently truncate the output.
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
    /// The configured thinking mode as the provider-neutral request field.
    /// Each adapter translates it: Anthropic to thinking plus effort, OpenAI to
    /// <c>reasoning_effort</c>. So the same value means the same thing on either provider.
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

        // provider-default sends nothing, so each model keeps its own default behaviour.
        return effort is null ? null : new ReasoningOptions { Effort = effort };
    }

    /// <summary>
    /// The client a run's configuration builds, with <paramref name="transport"/> instead
    /// of the network.
    /// This seam lets tests check a failed call through the real SDKs without an endpoint.
    /// </summary>
    internal static IChatClient CreateChatClient(IConfiguration configuration, HttpMessageHandler transport)
    {
        LlmProvider provider = LlmProviderParser.Parse(configuration[$"{LlmOptions.SectionName}:Provider"]);
        return CreateChatClient(provider, ReadOptions(configuration, provider), configuration, transport);
    }

    private static IChatClient CreateChatClient(
        LlmProvider provider,
        LlmOptions options,
        IConfiguration configuration,
        HttpMessageHandler? transport = null)
    {
        // Read the key by variable name, never hardcode it. An Anthropic run without a key
        // was already refused at registration. An OpenAI-compatible endpoint may need none.
        string? apiKey = configuration[options.ApiKeyEnvironmentVariable];

        IChatClient client = provider switch
        {
            LlmProvider.Anthropic => CreateAnthropicClient(options, apiKey, transport),
            LlmProvider.OpenAiCompatible => OpenAiCompatibleChatClient.Create(options, apiKey, transport),
            _ => throw new NotSupportedException(
                $"LLM provider '{options.Provider}' is not supported yet."),
        };

        return new FailureDescribingChatClient(client, options, apiKeyPresent: !string.IsNullOrWhiteSpace(apiKey));
    }

    private static IChatClient CreateAnthropicClient(LlmOptions options, string? apiKey, HttpMessageHandler? transport)
    {
        // Two initializers instead of one: BaseUrl is init-only, and its default is a
        // real URL, so passing null would blank it.
        // Set BaseUrl only when configured, which is the case for a proxy or gateway in
        // front of the API.
        HttpClient http = ModelRequestCountingHandler.CreateClient(transport);
        AnthropicClient client = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? new AnthropicClient { ApiKey = apiKey, HttpClient = http }
            : new AnthropicClient { ApiKey = apiKey, BaseUrl = options.BaseUrl, HttpClient = http };

        return client.AsIChatClient(options.Model);
    }
}
