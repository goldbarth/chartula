using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The single shipped <see cref="IChangelogModel"/>, backed by a
/// provider-agnostic <see cref="IChatClient"/>. Which concrete provider (and
/// model) the <see cref="IChatClient"/> talks to is decided in the composition
/// root; this class knows nothing about it. The rephrase prompt is owned by the
/// <see cref="IChangelogPromptBuilder"/>, so this type just wires it to the client.
/// </summary>
public sealed class ChatModel(IChatClient chat, IChangelogPromptBuilder promptBuilder) : IChangelogModel
{
    private readonly IChatClient _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    private readonly IChangelogPromptBuilder _promptBuilder =
        promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));

    public async Task<string> RephraseAsync(
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

        ChatResponse response = await _chat.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Text;
    }

    public async Task<FaithfulnessReport> CheckFaithfulnessAsync(
        FaithfulnessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ChatMessage> messages =
        [
            new(ChatRole.System,
                "Check whether every claim in the output is backed by the facts. " +
                "Report any claim that is not supported."),
            new(ChatRole.User,
                $"Facts:\n{FormatFacts(request.Facts)}\n\nOutput:\n{request.Output}"),
        ];

        ChatResponse<FaithfulnessReport> response =
            await _chat.GetResponseAsync<FaithfulnessReport>(messages, cancellationToken: cancellationToken);

        return response.TryGetResult(out FaithfulnessReport? report)
            ? report
            : new FaithfulnessReport(IsFaithful: false, UnsupportedClaims: []);
    }

    private static string FormatFacts(Facts.GroundedFacts facts)
        => string.Join('\n', facts.Statements.Select(static s => $"- {s}"));
}
