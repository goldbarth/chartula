using System.Globalization;
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
/// <remarks>
/// The prompt text lives in the <c>ChangelogPromptBuilder.Prompts.cs</c> partial;
/// this file only composes it.
/// </remarks>
public sealed partial class ChangelogPromptBuilder : IChangelogPromptBuilder
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
        system.AppendLine(SystemHeader);
        system.AppendLine(RuleRephraseOnly);
        system.AppendLine(RuleCategoryEstablished);
        system.AppendLine(RuleStaySparse);
        system.AppendLine(RuleNoPreamble);
        system.AppendLine(RuleConsistentVoice);
        system.Append(AudienceGuidance(audience));
        return system.ToString();
    }

    private static string AudienceGuidance(Audience audience) => audience switch
    {
        Audience.Technical => AudienceTechnical,
        Audience.Customer => AudienceCustomer,
        Audience.Product => AudienceProduct,
        _ => string.Format(CultureInfo.InvariantCulture, AudienceFallbackFormat, audience),
    };

    private static string FormatFacts(GroundedFacts facts)
        => string.Join('\n', facts.Statements.Select(static statement => $"- {statement}"));
}
