using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;

namespace Chartula.Core.Tests.Prompting;

public sealed class ChangelogPromptBuilderTests
{
    private readonly ChangelogPromptBuilder _builder = new();

    [Fact]
    public void Feeds_every_fact_into_the_user_prompt()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Feature: dark mode", "[2] Fix: crash on start"]), Audience.Customer);

        Assert.Contains("[1] Feature: dark mode", prompt.User);
        Assert.Contains("[2] Fix: crash on start", prompt.User);
    }

    [Fact]
    public void Passes_categories_and_breaking_flags_through_to_the_model_unchanged()
    {
        // The generator embeds category and the breaking marker into each fact;
        // the prompt carries them verbatim rather than deciding them.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Feature (breaking): remove the v1 endpoint"]), Audience.Technical);

        Assert.Contains("[1] Feature (breaking): remove the v1 endpoint", prompt.User);
    }

    [Fact]
    public void Instructs_the_model_to_rephrase_only_and_never_invent()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Feature: dark mode"]), Audience.Customer);

        Assert.Contains("Rephrase only", prompt.System);
        Assert.Contains("Never introduce a fact", prompt.System);
    }

    [Fact]
    public void Instructs_the_model_to_treat_category_and_markers_as_established()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Fix: a bug"]), Audience.Technical);

        Assert.Contains("category", prompt.System);
        Assert.Contains("breaking", prompt.System);
        Assert.Contains("(action required)", prompt.System);
        Assert.Contains("established", prompt.System);
    }

    [Fact]
    public void Instructs_the_model_to_write_in_one_consistent_voice()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Feature: dark mode"]), Audience.Customer);

        Assert.Contains("one consistent voice", prompt.System);
        Assert.Contains("author", prompt.System); // do not carry over an author's tone
    }

    [Fact]
    public void Instructs_the_model_to_stay_sparse_on_thin_facts()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Fix: a bug"]), Audience.Customer);

        Assert.Contains("thin", prompt.System);
        Assert.Contains("Do not pad", prompt.System);
    }

    [Fact]
    public void Does_not_pad_a_thin_fact_base_with_invented_content()
    {
        // A single, terse fact: the user prompt must carry that one line and
        // nothing our code invented around it.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Fix: a bug"]), Audience.Customer);

        Assert.Equal("- [1] Fix: a bug", prompt.User);
    }

    [Fact]
    public void Produces_an_empty_user_prompt_for_no_facts()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(new GroundedFacts([]), Audience.Customer);

        Assert.Equal(string.Empty, prompt.User);
    }

    [Fact]
    public void Faithfulness_prompt_carries_the_facts_the_output_and_the_meaning_level_instruction()
    {
        ChangelogPrompt prompt = _builder.BuildFaithfulnessPrompt(
            "This release closed a security hole.",
            new GroundedFacts(["Fix: correct an off-by-one in the parser"]));

        Assert.Contains("meaning-level", prompt.System);
        Assert.Contains("correct an off-by-one in the parser", prompt.User);
        Assert.Contains("This release closed a security hole.", prompt.User);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Customer)]
    [InlineData(Audience.Product)]
    public void Tailors_the_system_prompt_to_the_audience(Audience audience)
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["[1] Feature: dark mode"]), audience);

        Assert.Contains(audience.ToString(), prompt.System);
    }

    // Whitespace is collapsed so that a rule can be re-wrapped without breaking
    // a test. What is asserted below is that the rule is in the prompt, never
    // where its line breaks fall.
    private string SystemFor(Audience audience) => string.Join(
        ' ',
        _builder
            .BuildRephrasePrompt(new GroundedFacts(["[1] Feature: dark mode"]), audience)
            .System.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private string CustomerSystem() => SystemFor(Audience.Customer);

    // Issue #96: five renderings of one release on four models came back in five
    // structures while the prompt carried format rules. The structure is now put
    // together in code, so the prompt asks for one text per fact and nothing else.

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Customer)]
    [InlineData(Audience.Product)]
    public void Asks_for_one_entry_per_fact_id_and_nothing_around_it(Audience audience)
    {
        string system = SystemFor(audience);

        Assert.Contains("Write exactly one entry for every fact, under that fact's id", system);
        Assert.Contains("are put around your entries: never write them", system);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Customer)]
    [InlineData(Audience.Product)]
    public void Describes_no_structure_the_code_puts_around_the_entries(Audience audience)
    {
        // A format rule left in the prompt would ask the model for something that
        // code then overwrites, or worse, that code and model both write.
        string system = SystemFor(audience);

        Assert.DoesNotContain("What needs action", system);
        Assert.DoesNotContain("third-level heading", system);
        Assert.DoesNotContain("\"Also:\"", system);
        Assert.DoesNotContain("Nothing follows the last group", system);
        Assert.DoesNotContain("named by the group", system);
        Assert.DoesNotContain("named by the theme", system);
    }

    // The customer rules below are measured in goldbarth/chartula-evals; the counts
    // in the comments are what the rule's absence cost over 53 labelled entries.

    [Fact]
    public void Asks_a_customer_entry_for_a_label_that_is_not_the_start_of_the_sentence()
    {
        // The template's bold lead-in is a label. Code puts it in bold in front of
        // the text, so the model supplies it as a field of its own.
        string system = CustomerSystem();

        Assert.Contains("Give it a label", system);
        Assert.Contains("not the start of the sentence", system);
    }

    [Fact]
    public void Names_the_four_slots_an_entry_is_built_from()
    {
        string system = CustomerSystem();

        Assert.Contains("what the reader can observe", system);
        Assert.Contains("what they can now rely on", system);
        Assert.Contains("what they have to do", system);
        Assert.Contains("Two sentences", system);
    }

    [Fact]
    public void Forbids_opening_an_entry_on_the_work_that_was_done()
    {
        // 8 of 53 entries opened on the mechanism: "Added a categories section
        // to chartula.yaml...". The openings are named because naming the rule
        // alone did not move them.
        string system = CustomerSystem();

        Assert.Contains("never on the work that was done", system);
        Assert.Contains("\"Added\"", system);
        Assert.Contains("\"Fixed an issue where\"", system);
    }

    [Fact]
    public void Keeps_the_outcome_slot_out_of_the_parts_an_entry_may_drop()
    {
        // 7 of 20 entries on 2026-09-03 failed the outcome test, one of them
        // with no outcome sentence at all. The format described an entry as
        // four parts and then let any of the last three go, so an entry without
        // an outcome was following the prompt rather than breaking it.
        string system = CustomerSystem();

        Assert.Contains("Leave out the second or the fourth", system);
        Assert.Contains("what they can now rely on is always written", system);
    }

    [Fact]
    public void Keeps_out_a_claim_of_degree_the_reader_cannot_check()
    {
        // B3 rule 3. "defaults to a much higher value" passed the rule next to this
        // one, which forbids superlatives and marketing language and is neither.
        string system = CustomerSystem();

        Assert.Contains("claim of degree", system);
        Assert.Contains("check it against", system);
    }

    [Fact]
    public void Keeps_the_place_a_setting_is_reached_behind_the_outcome()
    {
        // Five of ten outcome failures on 2026-09-04 were entries about a setting
        // that ended on where it lives. The rule asking for the place was being
        // followed, not broken: nothing said the place is the fourth part, so it
        // was written where the outcome belongs.
        string system = CustomerSystem();

        Assert.Contains("That place is the fourth", system);
        Assert.Contains("never instead of it", system);
    }

    [Fact]
    public void Says_where_an_outcome_is_taken_from_before_testing_the_one_written()
    {
        // 10 of 24 entries on 2026-09-08 stated no outcome with the test below
        // already in the prompt, nine of them failing nothing else. A test
        // rejects a sentence and does not produce one, so the rule says where
        // the sentence comes from first, and names the fix case, where "the
        // fault is gone" is the opening restated - issue #122.
        string system = CustomerSystem();

        Assert.Contains("from their side of the change", system);
        Assert.Contains("no longer have to work around", system);
        Assert.Contains("never that the fault is gone", system);
        Assert.Contains("Take it from the facts of this change", system);
    }

    [Fact]
    public void Shows_a_finished_outcome_for_a_fix_and_for_a_new_capability()
    {
        // Every rule around the outcome says what to refuse, and entries kept
        // failing it with those rules in the prompt - issue #122. The examples
        // show the sentence instead, and say their subjects are invented, so
        // nothing in them is taken for a fact of the release.
        string system = CustomerSystem();

        Assert.Contains("one for a fix and one for a new capability", system);
        Assert.Contains("Their subjects are invented", system);
        Assert.Contains("so you no longer have to keep a local copy open", system);
        Assert.Contains("so you no longer have to export it", system);
    }

    [Fact]
    public void Opens_each_change_type_on_what_the_reader_meets()
    {
        // C1 of rubric/customer.md: a fix opens on what went wrong as the reader
        // ran into it, a feature on what they can now do, a breaking change on
        // what no longer works.
        string system = CustomerSystem();

        Assert.Contains("for a fix, what went wrong as they ran into it", system);
        Assert.Contains("for a breaking change, what no longer works", system);
        Assert.Contains("the opening is the fault as the reader ran into it", system);
    }

    [Fact]
    public void Leaves_out_an_outcome_the_facts_do_not_give_rather_than_invent_one()
    {
        // An always-written outcome against rephrase-only is a contradiction
        // whenever the facts carry none. The rubric's fact base implications
        // settle it: an unknown slot is omitted, never filled.
        string system = CustomerSystem();

        Assert.Contains("unless nothing in the facts of the change says what it was for", system);
        Assert.Contains("with nothing in its place", system);
    }

    [Fact]
    public void Names_a_condition_only_when_the_facts_give_one()
    {
        // C2 rules 3 and 4: "in some runs" gestures at a condition nobody can
        // place themselves in, and an unknown condition is not written as a guess.
        string system = CustomerSystem();

        Assert.Contains("a condition they can place themselves inside or outside", system);
        Assert.Contains("If the facts do not say, leave that part out", system);
    }

    [Fact]
    public void Gives_a_change_that_asks_something_an_action_and_the_outcome_after_it()
    {
        // C4 rule 4 and C3 rule 5: a breaking change always has something to do,
        // and its outcome is what the migration gets the reader. A change labelled
        // as asking something arrives marked, so it gets the same fourth part.
        string system = CustomerSystem();

        Assert.Contains("\"(breaking)\" or \"(action required)\" always has something to do", system);
        Assert.Contains("its fourth part is never left out", system);
        Assert.Contains("what holds once the reader has done it", system);
    }

    [Fact]
    public void Carries_the_test_that_decides_whether_a_closing_clause_is_an_outcome()
    {
        // The largest single failure, 19 of 53: "...so text completes properly"
        // is the opening negated. Naming the slot was not enough; the prompt has
        // to carry the test that decides the case.
        string system = CustomerSystem();

        Assert.Contains("strike the opening clause", system);
        Assert.Contains("restates the opening", system);
        Assert.Contains("negates it", system);
        Assert.Contains("Striking the clause is not the way out", system);
    }

    [Fact]
    public void Decides_an_expression_by_how_the_reader_would_have_met_it()
    {
        // 15 of 53 named something only a contributor would know: `categories`,
        // `chartula.yaml`, `docs/configuration.md`. This is a test rather than a
        // list of forbidden kinds, because the list is never finished - the
        // first expression outside it gets the wrong verdict.
        string system = CustomerSystem();

        Assert.Contains("ask how the reader would have met it", system);
        Assert.Contains("seen it in their own repository", system);
        Assert.Contains("reading the source or the developer documentation", system);
        Assert.Contains("named in prose", system);
    }

    [Fact]
    public void Says_whose_knowledge_counts()
    {
        // Without this the test above has no anchor: what is familiar to
        // whoever wrote the code is not familiar to whoever uses it.
        Assert.Contains("never someone who worked on it", CustomerSystem());
    }

    [Fact]
    public void Stops_an_entry_once_its_outcome_is_stated()
    {
        // Every rendering ran entries on past their outcome, at three to five
        // sentences where two is the limit. The action is the fourth part and
        // follows the outcome, so stopping at the outcome would cut it off.
        string system = CustomerSystem();

        Assert.Contains("stop once the outcome and, where there is one, what they have to do are stated", system);
        Assert.Contains("No superlatives, no marketing language", system);
    }

    [Fact]
    public void Requires_a_place_for_an_option_or_no_option_at_all()
    {
        // 8 of 53 announced a setting with nowhere to act on it: "a configurable
        // ceiling". The second half matters as much as the first - where the
        // facts carry no place, the entry leaves the option out.
        string system = CustomerSystem();

        Assert.Contains("say where it is set", system);
        Assert.Contains("leave the option out", system);
    }

    [Fact]
    public void Names_no_category_because_a_category_reaches_the_model_renamed()
    {
        // categories.names lets a user rename any category, and the fact
        // statements carry the configured display name. A rule naming one would
        // break for whoever renamed it.
        string system = CustomerSystem();

        Assert.DoesNotContain("a feature to", system);
        Assert.DoesNotContain("a fix to", system);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public void Keeps_the_customer_rules_out_of_the_other_audiences(Audience audience)
    {
        string system = SystemFor(audience);

        Assert.DoesNotContain("strike the opening clause", system);
        Assert.DoesNotContain("Give it a label", system);
    }

    // The technical and product rules follow the templates and rubrics of the same
    // name in goldbarth/chartula-evals. Neither has been measured against a
    // rendering yet, so the tests pin that each rule is present, not a count.

    [Fact]
    public void Asks_for_one_imperative_statement_per_change()
    {
        // C1 and C5 of rubric/technical.md: "Adds" is the shape every entry of
        // sonnet-5-out opens on, and several changes in one line is opus-5-out.
        string system = SystemFor(Audience.Technical);

        Assert.Contains("One statement about one change", system);
        Assert.Contains("verb in the imperative", system);
        Assert.Contains("Never \"Adds\", \"Added\" or \"Adding\"", system);
    }

    [Fact]
    public void Keeps_the_title_and_the_verification_block_out_of_a_technical_entry()
    {
        // C3 and B2: a carried-over title with its prefix, and the build and test
        // report sonnet-5-out put into 28 of its entries.
        string system = SystemFor(Audience.Technical);

        Assert.Contains("never carry a title over word for word", system);
        Assert.Contains("how the work was verified", system);
    }

    [Fact]
    public void Leaves_the_reference_of_a_technical_entry_to_the_rendering()
    {
        string system = SystemFor(Audience.Technical);

        Assert.Contains("Never write a pull request number or a link to one", system);
        Assert.Contains("the reference is added after your text", system);
    }

    [Fact]
    public void Asks_a_technical_entry_to_say_what_differs()
    {
        Assert.Contains("reads correctly without its heading", SystemFor(Audience.Technical));
    }

    [Fact]
    public void Builds_a_product_entry_from_what_changed_and_why_it_matters()
    {
        // The two slots of rubric/product.md, C1 and C2, with C2's strike test.
        string system = SystemFor(Audience.Product);

        Assert.Contains("what changed, and why it matters", system);
        Assert.Contains("never about the work that produced it", system);
        Assert.Contains("strike the first sentence", system);
        Assert.Contains("leave it out only when nothing in them does", system);
    }

    [Fact]
    public void Keeps_checkable_claims_and_reader_vocabulary_in_the_product_rendering()
    {
        // B3 and C3: a claim of impact needs a basis, and this reader never meets
        // the source.
        string system = SystemFor(Audience.Product);

        Assert.Contains("needs something in the entry the reader can check it against", system);
        Assert.Contains("configuration keys, file paths", system);
    }

    [Fact]
    public void Keeps_the_content_rules_for_every_audience()
    {
        // The format block is added to the customer audience, not swapped in for
        // the rules that apply everywhere.
        string system = CustomerSystem();

        Assert.Contains("Rephrase only", system);
        Assert.Contains("Do not pad", system);
        Assert.Contains("one consistent voice", system);
    }

    [Fact]
    public void Asks_the_customer_rendering_for_a_description()
    {
        string system = CustomerSystem();

        Assert.Contains("Also write the description", system);
        Assert.Contains("what this release is about", system);
        Assert.Contains("leave the description empty", system);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public void No_other_audience_is_asked_for_a_description(Audience audience)
    {
        // Nothing reads one back for them, so asking would spend tokens on a field
        // no output uses.
        Assert.DoesNotContain("write the description", SystemFor(audience));
    }
}
