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

    /// <summary>
    /// The shape of a technical rendering: Common Changelog, as adopted in
    /// <c>docs/output-format.md</c> of goldbarth/chartula-evals and judged by
    /// <c>rubric/technical.md</c> there.
    /// <para>
    /// What is decided rather than worded reaches the model as a fact: the group in
    /// brackets before each statement and the reference at its end are set by
    /// <see cref="Generation.GroundedFactsFactory"/>, so the rules below only say to
    /// use them as given. The release heading is not the model's either - the
    /// <c>CHANGELOG.md</c> composer writes it around this text.
    /// </para>
    /// <para>
    /// The format wants an author on every entry of a release with more than one
    /// contributor. The fact base carries no author, so nothing here asks for one
    /// rather than have the model guess.
    /// </para>
    /// </summary>
    private const string TechnicalFormat =
        """

        Write the technical section in this shape:
        - Group entries under third-level headings named by the group in brackets
        before each fact, in the order the facts give them, and only those that have
        entries. The group is established like the category: use it as given. There
        are no other headings: no release heading, which is written around this text,
        and never a heading for a single change.
        - One line per entry, one entry per change, and nothing nested under it. A
        line carrying several changes, or running to a paragraph about one, is wrong.
        - End each line with the reference given at the end of its fact, exactly as
        given. A fact that carries none gets none: never write a reference of your
        own.
        - Open the description on a verb in the imperative that completes "This
        release will": Add, Fix, Send, Remove. Never "Adds", "Added" or "Adding",
        and never the subject first.
        - Write the description rather than copy it: never carry a title over word
        for word, and never keep a commit-message prefix such as "feat:" or
        "fix(config):".
        - Say what is different, so the line reads correctly with its heading
        covered. A subject alone, such as "Configuration", is not a change.
        - A breaking change is prefixed with "**Breaking:**" and stands first in its
        group.
        - Nothing but entries stands under a heading: no paragraph on how the work
        was verified, such as build status or test counts, no nested list, no
        separator, no introduction, and nothing after the last group.
        - Class, method, file and configuration names stay, and so do links: this
        reader reads the source.
        """;

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
    /// The entry rules follow the slot table and the item axes of
    /// <c>rubric/customer.md</c> in the same repository. A rule may be stricter
    /// than its axis - two sentences, a fixed order - but never contrary to it:
    /// a prompt that asks for what the rubric fails makes every measurement a
    /// measurement of the contradiction.
    /// </para>
    /// <para>
    /// Two clauses are tests rather than lists, because a list of cases is
    /// never finished: what counts as an outcome, and what counts as an
    /// expression the reader has already met.
    /// </para>
    /// <para>
    /// The outcome carries a second clause that a test alone could not do the
    /// work of. Ten of 24 entries on 2026-09-08 still stated none, with the test
    /// already in the prompt, because a test rejects a sentence and does not
    /// produce one: a model that has written the mechanism keeps the mechanism,
    /// nothing having said what the sentence should have been. So the rule below
    /// first says where the outcome is taken from - the reader's side of the
    /// change, and for a fix what they no longer have to do about it - and the
    /// test follows as the check on what that produced. See issue #122.
    /// </para>
    /// <para>
    /// Every other rule here says what to refuse, and a rule saying where a
    /// sentence comes from still leaves the model to build it from a description.
    /// So the outcome rules close on two finished entries, a fix and a new
    /// capability, which is the shape the failures were in. The fix opens on what
    /// went wrong, as slot 1 of the rubric has it for a fix. Their subjects are
    /// invented and carry no number, no name and no setting, so an example breaks
    /// none of the rules around it and has nothing a rendering could borrow as
    /// a fact.
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
        own. That bullet is one entry and carries the observation alone: the other
        three parts of an entry do not apply to it.
        - Build each entry from four parts in this order: what the reader can
        observe, who or what it applies to, what they can now rely on, and what they
        have to do. Leave out the second or the fourth when it does not apply;
        what they can now rely on is always written, unless nothing in the facts of
        the change says what it was for: then it is left out, with nothing in its
        place.
        Two sentences, and stop once the outcome and, where there is one, what they
        have to do are stated: a sentence after that is either a second change or
        padding.
        - Say who or what a change applies to when it does not apply to every
        reader, as a condition they can place themselves inside or outside. If the
        facts do not say, leave that part out: a hint at a condition, such as "in
        some runs", tells the reader nothing.
        - A breaking change always has something to do, so its fourth part is never
        left out, and its outcome is what holds once the reader has done it, not
        what they lose without it.
        - Write plainly. No superlatives, no marketing language, and nothing about
        how much work a change was.
        - A claim of degree - faster, smaller, higher, more reliable - needs something
        in the entry the reader can check it against. Without a number or a basis,
        leave the claim out rather than soften it.
        - Open on what the reader observes, never on the work that was done: for a
        fix, what went wrong as they ran into it; for a new capability, what they
        can now do or see; for a breaking change, what no longer works the way it
        did. Do not begin with "Added", "We've added", "New support for", "Reworked",
        "Introduced" or "Fixed an issue where".
        - Write what the reader can now rely on from their side of the change,
        not from the change: what they no longer have to do, no longer have to
        check, no longer have to work around, or can now count on without looking.
        For a fix the opening is the fault as the reader ran into it, so the
        outcome is never that the fault is gone: it is what they no longer have to
        do about it. Take it from the facts of this change, which usually say what
        it was for.
        - The outcome must survive this test: strike the opening clause and read
        what is left. If it only restates the opening, negates it, or names a value
        or a mechanism, it is not an outcome. Say what the reader can now rely on
        instead. Striking the clause is not the way out.
        - Two entries in that shape follow, one for a fix and one for a new
        capability. Their subjects are invented: take the shape from them and never
        a word of their content.
          **Saving over a network drive**: A document saved to a network drive could
          lose the changes made while the connection dropped. Every change is kept
          now, so you no longer have to keep a local copy open as a backup while you
          work.
          **Sharing a report**: A report can now be shared as a link. Anyone you
          send it to can read it without an account, so you no longer have to
          export it and attach it first.
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
        set, as a place they can find rather than as a key. That place is the fourth
        part of the entry, so it comes after what the reader can now rely on and
        never instead of it: an entry that ends on where something is set has not
        said what setting it gets them. If the facts do not say where, leave the
        option out rather than announce it with no place.
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

    // The theme is not the model's to find - it arrives with each fact - so the
    // audience line no longer asks for grouping of its own.
    private const string AudienceProduct =
        "Audience: Product. The reader tracks what shipped and what it means for the product.";

    /// <summary>
    /// The shape of a product rendering: <c>product/thematic</c> in
    /// <c>docs/output-format.md</c> of goldbarth/chartula-evals, judged by
    /// <c>rubric/product.md</c> there. That rubric has no labelled corpus yet, so
    /// unlike the customer rules nothing here answers a counted failure.
    /// <para>
    /// The theme is established before the prompt, like the technical group: it is
    /// a lookup of allow-listed labels, not a judgement, and a model asked to
    /// classify would put a word in the document no fact gave it. See
    /// <see cref="Generation.GroundedFactsFactory"/>.
    /// </para>
    /// <para>
    /// Why a change matters is left out when nothing in its facts says, the same way
    /// the customer outcome is. The rubric fails such an entry, and that is the
    /// honest result: the alternative is a benefit no fact stated.
    /// </para>
    /// </summary>
    private const string ProductFormat =
        """

        Write the product section in this shape:
        - Group entries under third-level headings named by the theme in brackets
        before each fact, in the order the facts give them, and only those that have
        entries. The theme is established like the category: use it as given. There
        are no other headings, nothing but entries stands under a heading, and
        nothing follows the last one.
        - One bullet per entry, one entry per change.
        - Build each entry from two sentences in this order: what changed, and why it
        matters. Stop after the second.
        - What changed is a fact about the product as it now stands, never about the
        work that produced it. Do not write "Refactored", "Reworked" or "Introduced
        an abstraction for".
        - Why it matters is what the change means for the people the product serves,
        or for a decision the reader is tracking: what they can now plan, promise or
        stop spending. Take it from the facts of this change, which usually say what
        it was for, and leave it out only when nothing in them does, with nothing in
        its place.
        - Why it matters must survive this test: strike the first sentence and read
        what is left. If it only repeats the change, its mechanism or its negation,
        it does not say why the change matters.
        - A claim of impact or degree - cheaper, faster, more reliable, a fraction -
        needs something in the entry the reader can check it against: a number, a
        group affected, what held before. Without one, leave the claim out.
        - The reader tracks the product from outside its repository and never worked
        on it. Pull request numbers, commit hashes, issue references, author names,
        compare links, configuration keys, file paths, class or method names and
        concrete default values never appear. A setting is named in prose, by what it
        decides.
        """;

    private const string AudienceFallbackFormat = "Audience: {0}.";

    private const string FaithfulnessSystem =
        "You verify a changelog against the established facts. Flag any claim in " +
        "the output that the facts do not support - including meaning-level " +
        "distortions where the wording overstates or changes what happened (for " +
        "example, a bug fix described as a security fix). Report each unsupported " +
        "claim; if every claim is supported, report none.";

    private const string FaithfulnessUserFormat = "Facts:\n{0}\n\nOutput:\n{1}";
}
