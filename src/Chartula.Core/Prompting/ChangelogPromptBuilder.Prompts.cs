namespace Chartula.Core.Prompting;

/// <summary>
/// The prompt text for <see cref="ChangelogPromptBuilder"/>.
/// This partial holds only the strings the model sees. To change what the model is
/// told, edit them here. The composition lives in <c>ChangelogPromptBuilder.cs</c>.
/// <para>
/// Nothing here describes the structure of a rendering.
/// <see cref="Generation.RenderingComposer"/> adds headings, groups, order, markers and
/// references around the model's entries. The reason is #96: while the prompt still
/// carried format rules, five renderings of one release on four models came back in
/// five structures, two of them from the same model.
/// The strings here only say how the text of one entry is written.
/// </para>
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
        "- Each fact's category and any \"(breaking)\" or \"(action required)\" marker are " +
        "established. Use them as given; do not change, infer, or add them.";

    private const string RuleStaySparse =
        "- If the facts are thin, keep the output brief. Do not pad, speculate, " +
        "or invent detail to make it read fuller.";

    private const string RuleOneEntryPerFact =
        "- Each fact opens on its id in brackets. Write exactly one entry for every fact, " +
        "under that fact's id, and nothing else. Headings, groups, order, references and " +
        "markers such as \"Breaking:\" are put around your entries: never write them. " +
        "Leave the label and the description empty unless the rules below ask for them.";

    private const string RuleConsistentVoice =
        "- Write in one consistent voice throughout, no matter how each source was " +
        "written. Do not carry over an individual author's tone or phrasing.";

    private const string AudienceTechnical =
        "Audience: Technical. Keep precise terminology.";

    /// <summary>
    /// How a technical entry is written: the judgement rules of Common Changelog, as
    /// adopted in <c>docs/output-format.md</c> of goldbarth/chartula-evals and judged by
    /// <c>rubric/technical.md</c> there.
    /// The group, the breaking marker and the reference belong to the rendering, not
    /// to the entry.
    /// <para>
    /// The format requires an author on every entry of a release with more than one
    /// contributor. The fact base carries no author, so the prompt does not ask for one.
    /// Otherwise the model would guess.
    /// </para>
    /// </summary>
    private const string TechnicalFormat =
        """

        Write each technical entry in this shape:
        - One statement about one change. Several changes in one entry, or a paragraph
        about one, is wrong.
        - Open on a verb in the imperative that completes "This release will": Add, Fix,
        Send, Remove. Never "Adds", "Added" or "Adding", and never the subject first.
        - Write the description rather than copy it: never carry a title over word for
        word, and never keep a commit-message prefix such as "feat:" or "fix(config):".
        - Say what is different, so the entry reads correctly without its heading. A
        subject alone, such as "Configuration", is not a change.
        - Never write a pull request number or a link to one: the reference is added
        after your text.
        - Nothing about how the work was verified, such as build status or test counts.
        - Class, method, file and configuration names stay: this reader reads the source.
        Write each such name - a command, an option, a configuration key, a file, a type
        or a method - as a code span in backticks, spelled exactly as the facts spell it.
        Never put a word in backticks that the facts do not contain.
        """;

    private const string AudienceCustomer =
        "Audience: Customer. Focus on what changed for the user in plain language.";

    /// <summary>
    /// How a customer entry is written. The rules implement the item axes of
    /// <c>rubric/customer.md</c> in goldbarth/chartula-evals, where they are measured.
    /// A rule may be stricter than its axis, for example two sentences or a fixed order
    /// of parts. It must never contradict its axis: if the prompt asks for what the
    /// rubric fails, every measurement only measures that contradiction.
    /// <para>
    /// Each rule addresses a failure counted there over 53 entries and three renderings:
    /// <list type="bullet">
    /// <item>no outcome (19),</item>
    /// <item>an expression only a contributor would know (15),</item>
    /// <item>an opening on the mechanism (8),</item>
    /// <item>an option without its location (8).</item>
    /// </list>
    /// Two clauses are tests instead of lists, because a list of cases is never complete:
    /// what counts as an outcome, and what counts as an expression the reader already knows.
    /// </para>
    /// <para>
    /// The outcome rule first says where the outcome comes from: the reader's side of
    /// the change, and for a fix, what the reader no longer has to do about it.
    /// Then the test checks the result, ending on two finished entries with invented
    /// subjects. A test rejects a sentence but does not produce one (#122).
    /// </para>
    /// <para>
    /// No category is named here. A category reaches the model under its configured
    /// display name (<c>categories.names</c>), so a rule naming one would break for
    /// anyone who renamed it.
    /// </para>
    /// </summary>
    private const string CustomerFormat =
        """

        Write each customer entry in this shape:
        - Give it a label: a few words naming what the entry is about. The label is a
        name, not the start of the sentence, and the text does not repeat it.
        - Build the text from four parts in this order: what the reader can observe, who
        or what it applies to, what they can now rely on, and what they have to do.
        Leave out the second or the fourth when it does not apply; what they can now
        rely on is always written, unless nothing in the facts of the change says what
        it was for: then it is left out, with nothing in its place. Two sentences, and
        stop once the outcome and, where there is one, what they have to do are stated:
        a sentence after that is either a second change or padding.
        - Say who or what a change applies to when it does not apply to every reader, as
        a condition they can place themselves inside or outside. If the facts do not
        say, leave that part out: a hint at a condition, such as "in some runs", tells
        the reader nothing.
        - A fact marked "(breaking)" or "(action required)" always has something to do,
        so its fourth part is never left out, and a migration link belongs there. For a
        breaking change the outcome is what holds once the reader has done it, not what
        they lose without it.
        - Write plainly. No superlatives, no marketing language, and nothing about how
        much work a change was.
        - A claim of degree - faster, smaller, higher, more reliable - needs something in
        the entry the reader can check it against. Without a number or a basis, leave
        the claim out rather than soften it.
        - Open on what the reader observes, never on the work that was done: for a fix,
        what went wrong as they ran into it; for a new capability, what they can now do
        or see; for a breaking change, what no longer works the way it did. Do not begin
        with "Added", "We've added", "New support for", "Reworked", "Introduced" or
        "Fixed an issue where".
        - Write what the reader can now rely on from their side of the change, not from
        the change: what they no longer have to do, no longer have to check, no longer
        have to work around, or can now count on without looking. For a fix the opening
        is the fault as the reader ran into it, so the outcome is never that the fault is
        gone: it is what they no longer have to do about it. Take it from the facts of
        this change, which usually say what it was for.
        - The outcome must survive this test: strike the opening clause and read what is
        left. If it only restates the opening, negates it, or names a value or a
        mechanism, it is not an outcome. Say what the reader can now rely on instead.
        Striking the clause is not the way out.
        - Two entries in that shape follow, one for a fix and one for a new capability.
        Their subjects are invented: take the shape from them and never a word of their
        content.
          Label: Saving over a network drive
          Text: A document saved to a network drive could lose the changes made while
          the connection dropped. Every change is kept now, so you no longer have to keep
          a local copy open as a backup while you work.
          Label: Sharing a report
          Text: A report can now be shared as a link. Anyone you send it to can read it
          without an account, so you no longer have to export it and attach it first.
        - The reader is a user of the product this changelog is about, never someone who
        worked on it: what is familiar from writing the source does not count as
        familiar. For every expression that is not ordinary language, a name, an
        identifier, a value, a marker, a format, ask how the reader would have met it.
        Typed it themselves, seen it in their own repository, or seen it on screen while
        using the product: it stays. Met only by reading the source or the developer
        documentation: it goes, and a setting is named in prose instead. Pull request
        numbers, commit hashes, issue references, author names and compare links never
        appear.
        - If an entry offers the reader a setting or a decision, say where it is set, as
        a place they can find rather than as a key. That place is the fourth part of the
        entry, so it comes after what the reader can now rely on and never instead of
        it: an entry that ends on where something is set has not said what setting it
        gets them. If the facts do not say where, leave the option out rather than
        announce it with no place.
        """;

    /// <summary>
    /// The opening sentence of a published customer page.
    /// It rephrases facts the model already has, so it is requested in the same call as
    /// the entries instead of a second, paid call. It goes through the faithfulness
    /// check like everything else the model writes.
    /// <para>
    /// It may be empty, because a field with no source is omitted: never emitted empty,
    /// never filled with a placeholder.
    /// It is not simply the first entry: the description says what the release is about,
    /// an entry says what one change is.
    /// </para>
    /// </summary>
    private const string CustomerDescription =
        "\n\nAlso write the description: a single sentence on what this release is about, "
        + "drawn from the facts of this release and from nothing else. It is not the first "
        + "entry reworded and not a list of everything that changed. If the facts do not "
        + "support such a sentence, leave the description empty rather than writing a "
        + "filler one.";

    private const string AudienceProduct =
        "Audience: Product. The reader tracks what shipped and what it means for the product.";

    /// <summary>
    /// How a product entry is written: <c>product/thematic</c> in
    /// <c>docs/output-format.md</c> of goldbarth/chartula-evals, judged by
    /// <c>rubric/product.md</c> there.
    /// That rubric has no labelled corpus yet, so no rule here addresses a counted failure.
    /// The theme of an entry belongs to the rendering, not to the entry.
    /// <para>
    /// When the facts do not say why a change matters, the entry leaves it out, like
    /// the customer outcome. The rubric fails such an entry, and that is the correct
    /// result: the alternative is a benefit no fact stated.
    /// </para>
    /// </summary>
    private const string ProductFormat =
        """

        Write each product entry in this shape:
        - Two sentences in this order: what changed, and why it matters. Stop after the
        second.
        - What changed is a fact about the product as it now stands, never about the
        work that produced it. Do not write "Refactored", "Reworked" or "Introduced an
        abstraction for".
        - Why it matters is what the change means for the people the product serves, or
        for a decision the reader is tracking: what they can now plan, promise or stop
        spending. Take it from the facts of this change, which usually say what it was
        for, and leave it out only when nothing in them does, with nothing in its place.
        - Why it matters must survive this test: strike the first sentence and read what
        is left. If it only repeats the change, its mechanism or its negation, it does
        not say why the change matters.
        - A claim of impact or degree - cheaper, faster, more reliable, a fraction - needs
        something in the entry the reader can check it against: a number, a group
        affected, what held before. Without one, leave the claim out.
        - The reader tracks the product from outside its repository and never worked on
        it. Pull request numbers, commit hashes, issue references, author names, compare
        links, configuration keys, file paths, class or method names and concrete default
        values never appear. A setting is named in prose, by what it decides.
        """;

    private const string AudienceFallbackFormat = "Audience: {0}.";

    /// <summary>
    /// The instructions of the thorough check.
    /// Each claim names its fact by pull request number (the sentences from "A fact from a
    /// pull request" on). Before they existed, 5 of 38 flags over five runs of one release
    /// named a pull request, and the reviewer had to search the facts for the other 33.
    /// The number is taken from one fixed place only, because titles and descriptions
    /// mention issue numbers, which read like pull requests. Code still checks it against
    /// the fact base.
    /// </summary>
    private const string FaithfulnessSystem =
        "You verify a changelog against the established facts. Flag any claim in " +
        "the output that the facts do not support - including meaning-level " +
        "distortions where the wording overstates or changes what happened (for " +
        "example, a bug fix described as a security fix). Report each unsupported " +
        "claim; if every claim is supported, report none. " +
        "A fact from a pull request opens on its number in brackets, such as [#12]. " +
        "With each claim, give the number of the pull request whose fact the claim " +
        "rephrases, taken from those brackets and never from elsewhere in a fact. Give " +
        "none when the claim rephrases no single fact, such as a sentence about the " +
        "release as a whole.";

    private const string FaithfulnessUserFormat = "Facts:\n{0}\n\nOutput:\n{1}";
}
