using System.ClientModel;
using System.ClientModel.Primitives;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Chartula.Cli.Composition;

/// <summary>
/// Builds the <see cref="IChatClient"/> for an endpoint speaking the OpenAI
/// chat-completions dialect.
/// This is the only place that names the OpenAI package, which keeps it out of the domain.
/// </summary>
internal static class OpenAiCompatibleChatClient
{
    /// <summary>
    /// Stands in for an absent key.
    /// The endpoints this provider exists for (Ollama, LM Studio, llama.cpp, vLLM without
    /// <c>--api-key</c>) never read the Authorization header. But the credential type
    /// rejects an empty string in its constructor, so "no key needed" would crash before
    /// the first request.
    /// A hosted endpoint receives this value and answers 401. That is intended: the
    /// refusal comes from the endpoint, not from Chartula.
    /// </summary>
    private const string AbsentApiKeyPlaceholder = "no-api-key-configured";

    /// <summary>
    /// How long a single call may take before the client gives up.
    /// The SDK default of 100 seconds suits a hosted API. It fails the main case of this
    /// provider: a model on the user's own machine, where one call over a full changelog
    /// takes minutes, and the retry after the timeout only spends the time again.
    /// Measured on 2026-08-03: qwen2.5:14b on a 16 GB GPU exceeded 100 seconds on two of
    /// three audience texts and lost both.
    /// The value is generous, not tuned. A hung endpoint should still end the run, but
    /// the limit must be far above a slow answer, not close to it.
    /// </summary>
    private static readonly TimeSpan LocalEndpointTimeout = TimeSpan.FromMinutes(10);

    public static IChatClient Create(LlmOptions options, string? apiKey, HttpMessageHandler? transport = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{LlmOptions.BaseUrlVariable} is required when llm.provider is 'openai-compatible'. " +
                "There is no default, because an endpoint that is not named cannot be guessed - " +
                "for example http://localhost:11434/v1 for Ollama, http://localhost:1234/v1 for LM Studio.");
        }

        // Check the URL again instead of trusting it: options can be built without ReadOptions.
        Uri endpoint = EndpointUrl.Require(LlmOptions.BaseUrlVariable, options.BaseUrl);

        ApiKeyCredential credential = new(
            string.IsNullOrWhiteSpace(apiKey) ? AbsentApiKeyPlaceholder : apiKey);

        OpenAIClient client = new(credential, new OpenAIClientOptions
        {
            Endpoint = endpoint,
            NetworkTimeout = LocalEndpointTimeout,
            Transport = new HttpClientPipelineTransport(ModelRequestCountingHandler.CreateClient(transport)),
        });

        return client.GetChatClient(options.Model).AsIChatClient();
    }
}
