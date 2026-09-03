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
/// <para>
/// It also pins the shape of the customer rendering rather than leaving it to the
/// model, which is what issue #96 is about. Content rules and shape rules are
/// separate on purpose: the first five apply to every audience, the format block
/// only to the one whose shape is specified.
/// </para>
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

    public ChangelogPrompt BuildFaithfulnessPrompt(string output, GroundedFacts facts)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(facts);

        string user = string.Format(CultureInfo.InvariantCulture, FaithfulnessUserFormat, FormatFacts(facts), output);
        return new ChangelogPrompt(FaithfulnessSystem, user);
    }

    /// <remarks>
    /// Customer is the only audience whose shape is specified, so it is the only
    /// one that carries format rules, and the only one asked for a description.
    /// Technical and Product state tone alone until a specification exists for
    /// them - guessing a shape for an audience nobody has written one for would be
    /// the same defect as leaving it to the model, only harder to see.
    /// </remarks>
    private static string AudienceGuidance(Audience audience) => audience switch
    {
        Audience.Technical => AudienceTechnical,
        Audience.Customer => AudienceCustomer + CustomerFormat + CustomerDescription,
        Audience.Product => AudienceProduct,
        _ => string.Format(CultureInfo.InvariantCulture, AudienceFallbackFormat, audience),
    };

    private static string FormatFacts(GroundedFacts facts)
        => string.Join('\n', facts.Statements.Select(static statement => $"- {statement}"));
}
