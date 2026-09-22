using System.Diagnostics;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The single shipped <see cref="IChangelogModel"/>, backed by a
/// provider-agnostic <see cref="IChatClient"/>. Which concrete provider (and
/// model) the <see cref="IChatClient"/> talks to is decided in the composition
/// root; this class knows nothing about it. The rephrase prompt is owned by the
/// <see cref="IChangelogPromptBuilder"/>, so this type just wires it to the client.
/// Every call is the only place that sees real token usage, so it reports that usage
/// to <see cref="IRunMetrics"/>. Whether an answer counts is not decided here but in
/// <see cref="CallValidity"/>, the same way for every call.
/// </summary>
public sealed class ChatModel(
    IChatClient chat,
    IChangelogPromptBuilder promptBuilder,
    ChatModelOptions? options = null,
    IRunMetrics? metrics = null) : IChangelogModel
{
    private readonly IChatClient _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IChangelogPromptBuilder _promptBuilder =
        promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    private readonly ChatModelOptions _options = options ?? new ChatModelOptions();
    private readonly IRunMetrics _metrics = metrics ?? NullRunMetrics.Instance;

    // Fresh per call: the typed-response path clones and augments these, so a shared
    // instance would leak one call's response format into the next.
    private ChatOptions RequestOptions(LlmOperation operation)
        => operation == LlmOperation.FaithfulnessCheck
            ? new()
            {
                MaxOutputTokens = _options.MaxOutputTokens,
                ModelId = _options.CheckModelId,
                Reasoning = _options.CheckReasoning,
            }
            : new()
            {
                MaxOutputTokens = _options.MaxOutputTokens,
                Reasoning = _options.Reasoning,
            };

    public async Task<RenderedEntries> RephraseAsync(
        RephraseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ChangelogPrompt prompt = _promptBuilder.BuildRephrasePrompt(request.Facts, request.Audience);
        List<ChatMessage> messages =
        [
            new(ChatRole.System, prompt.System),
            new(ChatRole.User, prompt.User),
        ];

        // Recorded before it is judged: a call that does not count was still paid for.
        ChatResponse<RenderedEntries> response = await CallAsync(
            LlmOperation.Rephrase,
            () => _chat.GetResponseAsync<RenderedEntries>(messages, RequestOptions(LlmOperation.Rephrase), cancellationToken: cancellationToken));
        return CallValidity.Entries(prompt, response);
    }

    public async Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ChangelogPrompt prompt = _promptBuilder.BuildFaithfulnessPrompt(request.Output, request.Facts);
        List<ChatMessage> messages =
        [
            new(ChatRole.System, prompt.System),
            new(ChatRole.User, prompt.User),
        ];

        ChatResponse<FaithfulnessVerdict> response = await CallAsync(
            LlmOperation.FaithfulnessCheck,
            () => _chat.GetResponseAsync<FaithfulnessVerdict>(messages, RequestOptions(LlmOperation.FaithfulnessCheck), cancellationToken: cancellationToken));
        return CallValidity.Verdict(prompt, response);
    }

    /// <summary>
    /// Makes one model call and records it whatever the outcome: the time and the
    /// requests of a call that failed were spent too, and a run that was slow because
    /// its calls were retried has to be able to say so.
    /// </summary>
    private async Task<TResponse> CallAsync<TResponse>(LlmOperation operation, Func<Task<TResponse>> call)
        where TResponse : ChatResponse
    {
        using ModelCallAttempts attempts = ModelCallAttempts.Begin();
        long started = Stopwatch.GetTimestamp();
        try
        {
            TResponse response = await call();

            // Providers are not obliged to report usage; an unreported call is still a call.
            _metrics.RecordLlmCall(operation, new LlmCall(response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount)
            {
                CachedInputTokens = response.Usage?.CachedInputTokenCount,
                ReasoningTokens = response.Usage?.ReasoningTokenCount,
                Duration = Stopwatch.GetElapsedTime(started),
                Attempts = attempts.Observed,
            });
            return response;
        }
        catch (Exception)
        {
            _metrics.RecordLlmCall(operation, new LlmCall(null, null)
            {
                Duration = Stopwatch.GetElapsedTime(started),
                Attempts = attempts.Observed,
                Failed = true,
            });
            throw;
        }
    }
}
