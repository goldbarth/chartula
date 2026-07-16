using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Prompting;

/// <summary>
/// Default <see cref="IChangelogPromptBuilder"/>. The system prompt pins the model
/// to rephrasing established facts - it may not invent, and it must treat each
/// fact's category and breaking marker as given. Thin facts yield sparse output.
/// The user prompt carries only the facts; nothing is added to pad them.
/// </summary>
public sealed class ChangelogPromptBuilder : IChangelogPromptBuilder
{
    public ChangelogPrompt BuildRephrasePrompt(GroundedFacts facts, Audience audience)
    {
        ArgumentNullException.ThrowIfNull(facts);

        string system = BuildSystemPrompt(audience);
        string user = FormatFacts(facts);
        return new ChangelogPrompt(system, user);
    }

    private static string BuildSystemPrompt(Audience audience)
    {
        StringBuilder system = new();
        system.AppendLine(
            "You write release changelog entries by rephrasing established facts. " +
            "Follow these rules exactly:");
        system.AppendLine("- Rephrase only. Never introduce a fact, number, name, or detail " +
            "that is not in the provided list.");
        system.AppendLine("- Each fact's category and any \"(breaking)\" marker are established. " +
            "Use them as given; do not change, infer, or add them.");
        system.AppendLine("- If the facts are thin, keep the output brief. Do not pad, speculate, " +
            "or invent detail to make it read fuller.");
        system.AppendLine("- Do not add a preamble or a conclusion; output only the entries.");
        system.Append(AudienceGuidance(audience));
        return system.ToString();
    }

    private static string AudienceGuidance(Audience audience) => audience switch
    {
        Audience.Technical =>
            "Audience: Technical. Keep precise terminology, and call out breaking changes explicitly.",
        Audience.Customer =>
            "Audience: Customer. Focus on what changed for the user in plain language.",
        Audience.Product =>
            "Audience: Product. Group related changes by theme.",
        _ => $"Audience: {audience}.",
    };

    private static string FormatFacts(GroundedFacts facts)
        => string.Join('\n', facts.Statements.Select(static statement => $"- {statement}"));
}
