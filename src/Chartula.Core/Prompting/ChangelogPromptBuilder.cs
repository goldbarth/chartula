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
/// The structure of a rendering is not the prompt's: headings, groups, order,
/// markers and references are put around the entries in code, which is what issue
/// #96 is about. The rules here are about the words of an entry. The first five
/// apply to every audience, each format block only to its own.
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
        system.AppendLine(RuleOneEntryPerFact);
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
    /// Each audience carries the shape its template specifies. Only the customer
    /// rendering is asked for a description, because only the customer page has a
    /// field to put one in.
    /// </remarks>
    private static string AudienceGuidance(Audience audience) => audience switch
    {
        Audience.Technical => AudienceTechnical + TechnicalFormat,
        Audience.Customer => AudienceCustomer + CustomerFormat + CustomerDescription,
        Audience.Product => AudienceProduct + ProductFormat,
        _ => string.Format(CultureInfo.InvariantCulture, AudienceFallbackFormat, audience),
    };

    private static string FormatFacts(GroundedFacts facts)
        => string.Join('\n', facts.Statements.Select(static statement => $"- {statement}"));
}
