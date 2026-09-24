using System.Net;
using System.Text;
using Chartula.Cli.Configuration;
using Microsoft.Extensions.AI;

namespace Chartula.Cli.Composition;

/// <summary>
/// Turns a failed model call into a message that says which endpoint was asked and
/// what its answer means.
/// The SDK's own exception names neither the provider, the endpoint nor the model. A
/// run against another provider's base URL reported only <c>Status Code: NotFound</c> (#234).
/// The composition root is the only place that knows all three, so the message is
/// written here. The domain still sees a failure as an exception with a message.
/// </summary>
/// <param name="inner">The provider's client.</param>
/// <param name="llm">The resolved LLM options the client was built from.</param>
/// <param name="apiKeyPresent">Whether the key variable held a value, which decides what a 401 means.</param>
internal sealed class FailureDescribingChatClient(IChatClient inner, LlmOptions llm, bool apiKeyPresent)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using ModelErrorResponseHandler.Capture capture = ModelErrorResponseHandler.Begin();
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(Describe(options?.ModelId, capture.Last, ex), ex);
        }
    }

    /// <summary>
    /// The failure as lines: which endpoint answered what, what that usually means, and
    /// the endpoint's own message.
    /// Two audiences that fail the same way get the same text, so the output can print it once.
    /// </summary>
    private string Describe(string? requestedModel, ModelErrorResponse? error, Exception exception)
    {
        // The thorough check sends a model of its own only when it differs from llm.model.
        string model = requestedModel ?? llm.Model;
        string modelKey = model == llm.Model ? "llm.model" : "faithfulness.model";

        if (error is null)
        {
            // No answer at all: refused connection, DNS, TLS or a timeout. Name the
            // configured endpoint, because no request got through to name another.
            string configured = llm.BaseUrl ?? "its default endpoint";
            return $"{llm.Provider} at {configured} could not be reached for model '{model}': {TransportMessage(exception)}";
        }

        StringBuilder text = new($"{llm.Provider} at {error.Endpoint} answered {error.Status} for model '{model}'.");
        if (Meaning(error.StatusCode, model, modelKey) is { } meaning)
        {
            text.Append('\n').Append(meaning);
        }

        if (error.Message is { } said)
        {
            text.Append('\n').Append("The endpoint said: ").Append(said);
        }

        return text.ToString();
    }

    // Use the transport's own message. One SDK wraps it as "I/O exception", which is
    // less useful than "connection refused" or a failed name lookup.
    private static string TransportMessage(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException)
            {
                return current.Message;
            }
        }

        return exception.Message;
    }

    private string? Meaning(HttpStatusCode status, string model, string modelKey)
    {
        string key = llm.ApiKeyEnvironmentVariable;
        return status switch
        {
            // Without a base URL the endpoint is the provider's own, so only the model can be wrong.
            HttpStatusCode.NotFound when llm.BaseUrl is null =>
                $"The endpoint does not serve that model id - check {modelKey}.",
            HttpStatusCode.NotFound =>
                $"Either the endpoint does not serve that model id (check {modelKey}), or {LlmOptions.BaseUrlVariable} " +
                "is not the address of this provider's API - another provider's, or a wrong path such as a missing /v1.",
            HttpStatusCode.Unauthorized when !apiKeyPresent =>
                $"The endpoint needs a key, and {key} is not set.",
            HttpStatusCode.Unauthorized =>
                $"The endpoint rejected the key in {key}: it is expired, revoked or mistyped, or it is another provider's key.",
            HttpStatusCode.Forbidden =>
                $"The key in {key} is not allowed to make this request, for example because it has no access to model '{model}'.",
            _ => null,
        };
    }
}
