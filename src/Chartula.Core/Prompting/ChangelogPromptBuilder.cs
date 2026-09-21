using System.Globalization;
using System.Security.Cryptography;
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
    /// <summary>
    /// A SHA-256 hash of every instruction Chartula sends: the system prompt of each
    /// audience and of the thorough check, and the check's user template - the text
    /// Chartula controls, without the facts it wraps. It changes exactly when a
    /// prompt changes, so a run's output can be traced to the instructions behind it.
    /// </summary>
    public static string PromptHash { get; } = ComputePromptHash();

    private static string ComputePromptHash()
    {
        StringBuilder text = new();
        foreach (Audience audience in (Audience[])[Audience.Technical, Audience.Customer, Audience.Product])
        {
            // A separator no prompt contains, so moving text between two prompts
            // changes the hash too.
            text.Append(BuildSystemPrompt(audience)).Append('\0');
        }

        text.Append(FaithfulnessSystem).Append('\0').Append(FaithfulnessUserFormat);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return "sha256:" + Convert.ToHexStringLower(hash);
    }

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
