namespace Chartula.Core.Prompting;

/// <summary>
/// The prompt text for <see cref="ChangelogPromptBuilder"/>. This partial holds
/// only the strings the model is shown - to change what the model is told, edit
/// them here. The composition lives in <c>ChangelogPromptBuilder.cs</c>.
/// </summary>
public sealed partial class ChangelogPromptBuilder
{
    private const string SystemHeader =
        "You write release changelog entries by rephrasing established facts. " +
        "Follow these rules exactly:";

    private const string RuleRephraseOnly =
        "- Rephrase only. Never introduce a fact, number, name, or detail " +
        "that is not in the provided list.";

    private const string RuleCategoryEstablished =
        "- Each fact's category and any \"(breaking)\" marker are established. " +
        "Use them as given; do not change, infer, or add them.";

    private const string RuleStaySparse =
        "- If the facts are thin, keep the output brief. Do not pad, speculate, " +
        "or invent detail to make it read fuller.";

    private const string RuleNoPreamble =
        "- Do not add a preamble or a conclusion; output only the entries.";

    private const string AudienceTechnical =
        "Audience: Technical. Keep precise terminology and any links, " +
        "and call out breaking changes explicitly.";

    private const string AudienceCustomer =
        "Audience: Customer. Focus on what changed for the user in plain language.";

    private const string AudienceProduct =
        "Audience: Product. Group related changes by theme.";

    private const string AudienceFallbackFormat = "Audience: {0}.";
}
