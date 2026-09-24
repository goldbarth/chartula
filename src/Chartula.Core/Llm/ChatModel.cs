using System.Diagnostics;
using Chartula.Core.Observability;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The single shipped <see cref="IChangelogModel"/>, backed by a provider-agnostic
/// <see cref="IChatClient"/>.
/// <list type="bullet">
/// <item>The composition root decides which provider and model the client talks to.
/// This class knows nothing about it.</item>
/// <item><see cref="IChangelogPromptBuilder"/> owns the prompts. This class only
/// sends them to the client.</item>
/// <item>Only here is the real token usage visible, so every call reports it to
/// <see cref="IRunMetrics"/>.</item>
/// <item><see cref="CallValidity"/> decides whether an answer counts, the same way
/// for every call.</item>
/// </list>
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

    // Create new options per call. The typed-response path clones and extends them,
    // so a shared instance would leak one call's response format into the next.
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

        // CallAsync records the call before CallValidity judges it: a call that does
        // not count was still paid for.
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
    /// Makes one model call and records it, whatever the outcome.
    /// A failed call also spent time and requests, and a run that was slow because of
    /// retries has to be able to show that.
    /// </summary>
    private async Task<TResponse> CallAsync<TResponse>(LlmOperation operation, Func<Task<TResponse>> call)
        where TResponse : ChatResponse
    {
        using ModelCallAttempts attempts = ModelCallAttempts.Begin();
        long started = Stopwatch.GetTimestamp();
        try
        {
            TResponse response = await call();

            // Record the call even without usage: providers are not obliged to report it.
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
