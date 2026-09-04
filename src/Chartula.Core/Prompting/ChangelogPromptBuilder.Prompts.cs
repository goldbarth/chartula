using Chartula.Core.Generation;

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

    private const string RuleConsistentVoice =
        "- Write in one consistent voice and format throughout, no matter how each " +
        "source was written. Do not carry over an individual author's tone or phrasing.";

    private const string AudienceTechnical =
        "Audience: Technical. Keep precise terminology and any links, " +
        "and call out breaking changes explicitly.";

    private const string AudienceCustomer =
        "Audience: Customer. Focus on what changed for the user in plain language.";

    /// <summary>
    /// The shape of a customer rendering. Without it the structure of the
    /// product's main artefact is decided by whichever model is configured, and
    /// the same release renders differently twice in a row - see issue #96.
    /// <para>
    /// These rules implement the specification in <c>docs/output-format.md</c>
    /// of goldbarth/chartula-evals, which is also where they are measured. Each
    /// answers a failure counted there over 53 entries and three renderings: no
    /// outcome (19), an expression only a contributor would know (15), an
    /// opening on the mechanism (8), an option with no place (8). Three more
    /// failed in every rendering measured: a change carried by no entry at all,
    /// an entry that asks something buried below entries that do not, and
    /// entries that run on past their outcome.
    /// </para>
    /// <para>
    /// Two clauses are tests rather than lists, because a list of cases is
    /// never finished: what counts as an outcome, and what counts as an
    /// expression the reader has already met.
    /// </para>
    /// <para>
    /// No category is named here. A category reaches the model under its
    /// configured display name - <c>categories.names</c> - so a rule naming one
    /// would break for anyone who renamed it, and the groups are defined by what
    /// a change is rather than by what it is called. Which changes appear at all
    /// is decided before this prompt, by the user-visible filter in
    /// <see cref="Generation.GroundedFactsFactory"/>; the rule below only says
    /// that what does arrive has to be carried.
    /// </para>
    /// </summary>
    private const string CustomerFormat =
        """

        Write the customer section in this shape:
        - Group entries under these third-level headings, in this order, and only
        those that have entries: "What needs action" (anything the reader has to
        do), "What's New" (capabilities that did not exist before), "What's Changed"
        (behaviour that existed and now works differently), "Bug Fixes".
        - Carry every fact the reader could come into contact with by using the
        product: running something, looking at an output, meeting different
        behaviour, setting something new. Leaving one out is the more expensive
        mistake, because a reader cannot ask about what they were never told. Drop a
        fact only when nothing the reader does could bring them into contact with
        it.
        - Anything the reader has to do about a change belongs under "What needs
        action" whatever its category: their setup stops working, their output
        changes under them, they have to move or rename something. A breaking change
        always belongs there and is labelled "Breaking:". An optional setting costs
        nothing if ignored and stays in its own group.
        - Every entry that asks something of the reader stands above every entry that
        does not, whichever group each of them sits in. One such entry below one that
        asks nothing is wrong even when the groups themselves are in order. A breaking
        change comes first of all.
        - Nothing follows the last group. A migration link belongs in its entry, not
        in a closing line.
        - One bullet per entry, one entry per change. A bold lead-in is a label, not
        the start of the sentence.
        - Changes the reader would not act on are gathered into one closing bullet of
        their group, opening with "Also:", rather than each taking a bullet of its
        own. That bullet is one entry and carries the observation alone: the three
        parts below do not apply to it.
        - Build each entry from four parts in this order: what the reader can
        observe, who or what it applies to, what they can now rely on, and what they
        have to do. Leave out the second or the fourth when it does not apply;
        what they can now rely on is always written. Two sentences, and stop once
        the outcome is stated: a sentence after it is either a second change or
        padding.
        - Write plainly. No superlatives, no marketing language, and nothing about
        how much work a change was.
        - A claim of degree - faster, smaller, higher, more reliable - needs something
        in the entry the reader can check it against. Without a number or a basis,
        leave the claim out rather than soften it.
        - Open on what the reader observes, never on the work that was done. Do not
        begin with "Added", "We've added", "New support for", "Reworked",
        "Introduced" or "Fixed an issue where".
        - The outcome must survive this test: strike the opening clause and read
        what is left. If it only restates the opening, negates it, or names a value
        or a mechanism, it is not an outcome. Say what the reader can now rely on
        instead. Striking the clause is not the way out.
        - The reader is a user of the product this changelog is about, never someone
        who worked on it: what is familiar from writing the source does not count as
        familiar. For every expression that is not ordinary language, a name, an
        identifier, a value, a marker, a format, ask how the reader would have met
        it. Typed it themselves, seen it in their own repository, or seen it on
        screen while using the product: it stays. Met only by reading the source or
        the developer documentation: it goes, and a setting is named in prose
        instead. Pull request numbers, commit hashes, issue references, author names
        and compare links never appear.
        - If an entry offers the reader a setting or a decision, say where it is
        set, as a place they can find rather than as a key. If the facts do not say
        where, leave the option out rather than announce it with no place.
        """;

    /// <summary>
    /// The opening line of a published customer page. The description is a
    /// rephrasing of facts already in front of the model, so it is asked for in
    /// the same call as the entries rather than paid for in a second one, and it
    /// goes through the faithfulness check on the same footing as everything else
    /// the model writes.
    /// <para>
    /// The label is <see cref="ReleaseDescription.Label"/> rather than a literal,
    /// because the same string is read back off the front of the output: one
    /// constant, both ends. Nothing is generated from it at runtime - a const is
    /// inlined at compile time - so this creates no dependency on generation.
    /// </para>
    /// <para>
    /// Why the line may be left out: a field with no source is omitted, never
    /// emitted empty and never filled with a placeholder. And why it is not simply
    /// the first entry: the description says what the release is about, an entry
    /// says what one change is, so a description built from one entry would leave
    /// the ordering of the entries to decide the summary.
    /// </para>
    /// </summary>
    private const string CustomerDescription =
        "\n\nBefore the entries, write one line beginning with \"" + ReleaseDescription.Label
        + "\" followed by a single sentence on what this release is about, drawn from "
        + "the facts of this release and from nothing else. Then a blank line, then "
        + "the entries. This line is part of the format, not the preamble the rules "
        + "above forbid. It is not the first entry reworded and not a list of "
        + "everything that changed. If the facts do not support such a sentence, "
        + "leave the line out entirely rather than writing an empty or filler one.";

    private const string AudienceProduct =
        "Audience: Product. Group related changes by theme.";

    private const string AudienceFallbackFormat = "Audience: {0}.";

    private const string FaithfulnessSystem =
        "You verify a changelog against the established facts. Flag any claim in " +
        "the output that the facts do not support - including meaning-level " +
        "distortions where the wording overstates or changes what happened (for " +
        "example, a bug fix described as a security fix). Report each unsupported " +
        "claim; if every claim is supported, report none.";

    private const string FaithfulnessUserFormat = "Facts:\n{0}\n\nOutput:\n{1}";
}
