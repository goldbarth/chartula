using System.ClientModel;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Chartula.Cli.Composition;

/// <summary>
/// Builds the <see cref="IChatClient"/> for an endpoint speaking the OpenAI
/// chat-completions dialect. This is the only place the OpenAI package is named,
/// which is what keeps it out of the domain.
/// </summary>
internal static class OpenAiCompatibleChatClient
{
    /// <summary>
    /// Stands in for an absent key. The endpoints this provider exists for - Ollama,
    /// LM Studio, llama.cpp, vLLM without <c>--api-key</c> - never read the
    /// Authorization header, but the credential type rejects an empty string in its
    /// constructor, which would turn "no key needed" into a crash before a single
    /// request went out. A hosted endpoint sees this value and answers 401, which is
    /// the intended outcome: the refusal comes from the endpoint, not from us.
    /// </summary>
    private const string AbsentApiKeyPlaceholder = "no-api-key-configured";

    /// <summary>
    /// How long a single call may take before the client gives up. The SDK default is
    /// 100 seconds, which suits a hosted API and fails the case this provider exists
    /// for: a model running on the user's own machine, where one call over a full
    /// changelog takes minutes, and where the retry that follows the timeout only
    /// spends the time again. Measured on 2026-08-03: qwen2.5:14b on a 16 GB GPU
    /// exceeded 100 seconds on two of three audience texts and lost both.
    /// Generous rather than tuned - a hung endpoint should still end the run, but the
    /// ceiling has to be far above a slow answer, not near it.
    /// </summary>
    private static readonly TimeSpan LocalEndpointTimeout = TimeSpan.FromMinutes(10);

    public static IChatClient Create(LlmOptions options, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "llm.baseUrl is required when llm.provider is 'openai-compatible'. " +
                "There is no default, because an endpoint that is not named cannot be guessed - " +
                "for example http://localhost:11434/v1 for Ollama, http://localhost:1234/v1 for LM Studio.");
        }

        // The scheme is checked, not just the parse: 'localhost:11434' parses as an
        // absolute URI whose scheme is 'localhost' and whose path is '11434', so a
        // forgotten http:// would otherwise be accepted here and fail much later,
        // somewhere that no longer mentions the setting that caused it.
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Invalid llm.baseUrl '{options.BaseUrl}'. Expected an absolute http or https URL, " +
                "for example http://localhost:11434/v1.");
        }

        ApiKeyCredential credential = new(
            string.IsNullOrWhiteSpace(apiKey) ? AbsentApiKeyPlaceholder : apiKey);

        OpenAIClient client = new(credential, new OpenAIClientOptions
        {
            Endpoint = endpoint,
            NetworkTimeout = LocalEndpointTimeout,
        });

        return client.GetChatClient(options.Model).AsIChatClient();
    }
}
