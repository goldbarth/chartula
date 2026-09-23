using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Prompting;

/// <summary>
/// Default <see cref="IChangelogPromptBuilder"/>.
/// The system prompt restricts the model to rephrasing established facts. It may not
/// invent, and it must take each fact's category and breaking marker as given.
/// Thin facts produce sparse output.
/// The user prompt carries only the facts, with nothing added to pad them.
/// <para>
/// The prompt does not define the structure of a rendering. Code adds headings, groups,
/// order, markers and references around the entries (#96).
/// The rules here are about the wording of an entry. The first five rules apply to
/// every audience, each format block only to its own audience.
/// </para>
/// </summary>
/// <remarks>
/// The prompt text lives in the <c>ChangelogPromptBuilder.Prompts.cs</c> partial.
/// This file only composes it.
/// </remarks>
public sealed partial class ChangelogPromptBuilder : IChangelogPromptBuilder
{
    /// <summary>
    /// A SHA-256 hash of every instruction Chartula sends: the system prompt of each
    /// audience, the system prompt of the thorough check, and the check's user template.
    /// It covers the text Chartula controls, without the facts inside it.
    /// It changes exactly when a prompt changes, so a run's output can be traced to
    /// the instructions behind it.
    /// </summary>
    public static string PromptHash { get; } = ComputePromptHash();

    private static string ComputePromptHash()
    {
        StringBuilder text = new();
        foreach (Audience audience in (Audience[])[Audience.Technical, Audience.Customer, Audience.Product])
        {
            // Separate the prompts with a character no prompt contains, so moving text
            // from one prompt to another also changes the hash.
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

        // Use one line ending on every platform. AppendLine writes "\r\n" on Windows,
        // and the format blocks are raw literals with the line endings of the checked-out
        // source file. Without this, the same build would send and hash a different
        // prompt per platform.
        return system.ToString().ReplaceLineEndings("\n");
    }

    public ChangelogPrompt BuildFaithfulnessPrompt(string output, GroundedFacts facts)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(facts);

        string user = string.Format(CultureInfo.InvariantCulture, FaithfulnessUserFormat, FormatFacts(facts), output);
        return new ChangelogPrompt(FaithfulnessSystem, user);
    }

    /// <remarks>
    /// Each audience gets the format block its template specifies.
    /// Only the customer rendering requests a description, because only the customer
    /// page has a field for it.
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
