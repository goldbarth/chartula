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
/// to <see cref="IRunMetrics"/>.
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

    // No real tokenizer packs prose, code and JSON into fewer than roughly 4
    // characters per token; dividing by double that leaves wide margin and still
    // turns "characters Chartula sent" into a token count no tokenizer can
    // undercut. A reported count below it is proof an endpoint truncated the
    // prompt, not a guess (#85).
    private const int MaxCharsPerToken = 8;

    // Fresh per call: the typed-response path clones and augments these, so a shared
    // instance would leak one call's response format into the next.
    private ChatOptions RequestOptions()
        => new()
        {
            MaxOutputTokens = _options.MaxOutputTokens,
            RawRepresentationFactory = _options.RawRepresentationFactory,
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

        ChatResponse<RenderedEntries> response = await _chat.GetResponseAsync<RenderedEntries>(
            messages, RequestOptions(), cancellationToken: cancellationToken);
        Record(LlmOperation.Rephrase, response.Usage);

        // Unlike the thorough check, an unreadable answer has no honest partial form:
        // without the entries there is no rendering. The generator turns this into a
        // failed audience that says why.
        if (!response.TryGetResult(out RenderedEntries? entries) || entries.Entries is null)
        {
            throw new InvalidOperationException("the model's answer did not match the expected entry format");
        }

        return entries;
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

        ChatResponse<FaithfulnessVerdict> response =
            await _chat.GetResponseAsync<FaithfulnessVerdict>(
                messages, RequestOptions(), cancellationToken: cancellationToken);
        Record(LlmOperation.FaithfulnessCheck, response.Usage);
        EnsurePromptReachedTheModel(prompt, response.Usage);

        // A check we cannot read is not a check that passed. Both failures below leave
        // the output unverified, and saying so is the only honest result - an empty
        // claim list would read as a clean check.
        if (!response.TryGetResult(out FaithfulnessVerdict? verdict))
        {
            return FaithfulnessReport.NotEvaluated("the response did not match the expected format");
        }

        IReadOnlyList<string> claims = verdict.UnsupportedClaims ?? [];
        if (!verdict.IsFaithful && claims.Count == 0)
        {
            return FaithfulnessReport.NotEvaluated("the model called the output unfaithful but listed no claims");
        }

        return FaithfulnessReport.Checked(claims);
    }

    // A verdict is only worth reading if the whole prompt reached the model (#94).
    // An endpoint that silently truncates to fit its context window still answers,
    // and a schema-enforcing one still answers cleanly (#86) - so this runs before
    // the verdict is even inspected. An endpoint that reports the untruncated
    // length regardless of what it actually saw cannot be caught this way; that
    // gap is real; it is not closed here.
    private static void EnsurePromptReachedTheModel(ChangelogPrompt prompt, UsageDetails? usage)
    {
        if (usage?.InputTokenCount is not { } reported)
        {
            return;
        }

        long characters = prompt.System.Length + prompt.User.Length;
        long minimumTokens = characters / MaxCharsPerToken;
        if (reported < minimumTokens)
        {
            throw new InvalidOperationException(
                $"the endpoint reported {reported} input tokens for a prompt of {characters} characters, "
                + $"which no tokenizer produces fewer than {minimumTokens} tokens for - the endpoint's "
                + "context window is too small for what Chartula sends. See \"The context window is the "
                + "first thing to get right\" in docs/configuration.md.");
        }
    }

    // Providers are not obliged to report usage; an unreported call is still a call.
    private void Record(LlmOperation operation, UsageDetails? usage)
        => _metrics.RecordLlmCall(
            operation,
            usage?.InputTokenCount,
            usage?.OutputTokenCount);
}
