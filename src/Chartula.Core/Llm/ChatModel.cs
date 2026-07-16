using Microsoft.Extensions.AI;

namespace Chartula.Core.Llm;

/// <summary>
/// The single shipped <see cref="IChangelogModel"/>, backed by a
/// provider-agnostic <see cref="IChatClient"/>. Which concrete provider (and
/// model) the <see cref="IChatClient"/> talks to is decided in the composition
/// root; this class knows nothing about it.
/// </summary>
/// <remarks>
/// The prompts here are deliberately minimal placeholders. Prompt design is its
/// own issue; this type exists to fix the seam, not to finalize the wording.
/// </remarks>
public sealed class ChatModel(IChatClient chat) : IChangelogModel
{
    private readonly IChatClient _chat = chat ?? throw new ArgumentNullException(nameof(chat));

    public async Task<string> RephraseAsync(
        RephraseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ChatMessage> messages =
        [
            new(ChatRole.System,
                "You rephrase established facts into changelog prose. Rephrase only; " +
                "never introduce a fact that is not in the provided list. Tailor the " +
                $"wording for the {request.Audience} audience."),
            new(ChatRole.User, FormatFacts(request.Facts)),
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
