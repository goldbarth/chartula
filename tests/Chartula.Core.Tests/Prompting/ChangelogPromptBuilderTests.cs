using Chartula.Core.Facts;
using Chartula.Core.Generation;
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
            new GroundedFacts(["Feature: dark mode", "Fix: crash on start"]), Audience.Customer);

        Assert.Contains("Feature: dark mode", prompt.User);
        Assert.Contains("Fix: crash on start", prompt.User);
    }

    [Fact]
    public void Passes_categories_and_breaking_flags_through_to_the_model_unchanged()
    {
        // The generator embeds category and the breaking marker into each fact;
        // the prompt carries them verbatim rather than deciding them.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature (breaking): remove the v1 endpoint"]), Audience.Technical);

        Assert.Contains("Feature (breaking): remove the v1 endpoint", prompt.User);
    }

    [Fact]
    public void Instructs_the_model_to_rephrase_only_and_never_invent()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature: dark mode"]), Audience.Customer);

        Assert.Contains("Rephrase only", prompt.System);
        Assert.Contains("Never introduce a fact", prompt.System);
    }

    [Fact]
    public void Instructs_the_model_to_treat_category_and_breaking_as_established()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Fix: a bug"]), Audience.Technical);

        Assert.Contains("category", prompt.System);
        Assert.Contains("breaking", prompt.System);
        Assert.Contains("established", prompt.System);
    }

    [Fact]
    public void Instructs_the_model_to_write_in_one_consistent_voice_and_format()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature: dark mode"]), Audience.Customer);

        Assert.Contains("one consistent voice", prompt.System);
        Assert.Contains("author", prompt.System); // do not carry over an author's tone
    }

    [Fact]
    public void Instructs_the_model_to_stay_sparse_on_thin_facts()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Fix: a bug"]), Audience.Customer);

        Assert.Contains("thin", prompt.System);
        Assert.Contains("Do not pad", prompt.System);
    }

    [Fact]
    public void Does_not_pad_a_thin_fact_base_with_invented_content()
    {
        // A single, terse fact: the user prompt must carry that one line and
        // nothing our code invented around it.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Fix: a bug"]), Audience.Customer);

        Assert.Equal("- Fix: a bug", prompt.User);
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
            new GroundedFacts(["Feature: dark mode"]), audience);

        Assert.Contains(audience.ToString(), prompt.System);
    }

    // The tests below assert the shape the customer prompt asks for, not the
    // content it produces. Issue #96: the structure of the product's main
    // artefact was decided by whichever model was configured, and the tests as
    // they stood would not have caught any of the three variants recorded there.
    // Each rule is measured in goldbarth/chartula-evals; the counts in the
    // comments are what the rule's absence cost over 53 labelled entries.

    // Whitespace is collapsed so that a rule can be re-wrapped without breaking
    // a test. What is asserted below is that the rule is in the prompt, never
    // where its line breaks fall.
    private string CustomerSystem() => string.Join(
        ' ',
        _builder
            .BuildRephrasePrompt(new GroundedFacts(["Feature: dark mode"]), Audience.Customer)
            .System.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void Names_the_customer_groups_and_their_order()
    {
        string system = CustomerSystem();

        Assert.Contains("What needs action", system);
        Assert.Contains("What's New", system);
        Assert.Contains("What's Changed", system);
        Assert.Contains("Bug Fixes", system);
        Assert.True(
            system.IndexOf("What needs action", StringComparison.Ordinal)
            < system.IndexOf("What's New", StringComparison.Ordinal),
            "the group a reader has to act on has to be named before the rest");
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
    public void Puts_what_the_reader_must_act_on_above_what_they_can_ignore()
    {
        // Judged as B1 in the evaluation harness, and written in output-format.md
        // rule 10. An entry saying separate marketing files are no longer written
        // sat below entries that ask nothing, and one such entry fails the whole
        // rendering however good the rest of it is.
        string system = CustomerSystem();

        Assert.Contains("stands above every entry that", system);
        Assert.Contains("comes first of all", system);
    }

    [Fact]
    public void Ends_the_document_with_its_last_group()
    {
        // output-format.md rule 5: a link under the last group is the one thing the
        // reader must act on, in the one place the ordering rule cannot reach.
        Assert.Contains("Nothing follows the last group", CustomerSystem());
    }

    [Fact]
    public void Collapses_what_the_reader_would_not_act_on_into_one_line()
    {
        // output-format.md rule 11, judged as B3 rule 2. The exemption matters as
        // much as the rule: the outcome slot is compulsory since #106, and the
        // collapsed line is the one entry that carries none.
        string system = CustomerSystem();

        Assert.Contains("\"Also:\"", system);
        Assert.Contains("carries the observation alone", system);
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
        // what no longer works. A prompt opening a fix on the repaired state
        // would ask for what the rubric does not.
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
    public void Gives_a_breaking_change_an_action_and_the_outcome_after_it()
    {
        // C4 rule 4 and C3 rule 5: a breaking change always has something to do,
        // and its outcome is what the migration gets the reader.
        string system = CustomerSystem();

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
        string system = CustomerSystem();

        Assert.Contains("never someone who worked on it", system);
    }

    [Fact]
    public void Requires_every_noticeable_fact_to_be_carried()
    {
        // The most expensive failure and the one every rendering made: a change
        // the reader could meet, with no entry at all. One rendering dropped
        // every feature of the release.
        string system = CustomerSystem();

        Assert.Contains("Carry every fact the reader could come into contact with", system);
        Assert.Contains("cannot ask about what they were never told", system);
    }

    [Fact]
    public void Routes_anything_the_reader_must_act_on_into_the_first_group()
    {
        // Two renderings buried an entry that asks something below entries that
        // ask nothing. Mapping by category alone does not catch it: the entry
        // was a feature by category and still cost the reader their setup.
        string system = CustomerSystem();

        Assert.Contains("whatever its category", system);
        Assert.Contains("their setup stops working", system);
    }

    [Fact]
    public void Stops_an_entry_once_its_outcome_is_stated()
    {
        // Every rendering ran entries on past their outcome, at three to five
        // sentences where two is the limit.
        string system = CustomerSystem();

        // The action is the fourth part and follows the outcome, so stopping at
        // the outcome would cut it off - B3 counts only what trails behind both.
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
    public void Says_where_a_breaking_change_goes_and_how_it_is_labelled()
    {
        string system = CustomerSystem();

        Assert.Contains("Breaking:", system);
        Assert.Contains("always belongs there", system);
    }

    [Fact]
    public void Names_no_category_because_a_category_reaches_the_model_renamed()
    {
        // categories.names lets a user rename any category, and the fact
        // statements carry the configured display name. A rule naming one would
        // break for whoever renamed it, so the groups are defined by what a
        // change is rather than by what it is called.
        string system = CustomerSystem();

        Assert.DoesNotContain("a feature to", system);
        Assert.DoesNotContain("a fix to", system);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public void Keeps_the_customer_shape_out_of_the_other_audiences(Audience audience)
    {
        // Each audience has a template of its own. The customer groups are about
        // what the reader has to do, which neither other reader is asked.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature: dark mode"]), audience);

        Assert.DoesNotContain("What needs action", prompt.System);
        Assert.DoesNotContain("strike the opening clause", prompt.System);
    }

    // The technical and product prompts follow the templates and rubrics of the
    // same name in goldbarth/chartula-evals. Neither has been measured against a
    // rendering yet, so the tests pin that each rule is present, not a count.
    private string SystemFor(Audience audience) => string.Join(
        ' ',
        _builder
            .BuildRephrasePrompt(new GroundedFacts(["[Added] Feature: dark mode"]), audience)
            .System.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void Takes_the_technical_group_and_reference_as_given()
    {
        // Both are decided when the facts are built. A prompt that asked the model
        // to map a category onto a group would move a classification into it.
        string system = SystemFor(Audience.Technical);

        Assert.Contains("named by the group in brackets", system);
        Assert.Contains("exactly as given", system);
        Assert.Contains("never write a reference of your own", system);
    }

    [Fact]
    public void Leaves_the_release_heading_to_the_changelog_file()
    {
        // The composer writes "## VERSION - DATE". A second one from the model
        // would be a heading the format does not define - B1 of the rubric.
        Assert.Contains("no release heading", SystemFor(Audience.Technical));
    }

    [Fact]
    public void Asks_for_one_imperative_line_per_change()
    {
        // C1 and C5 of rubric/technical.md: "Adds" is the shape every entry of
        // sonnet-5-out opens on, and several changes in one line is opus-5-out.
        string system = SystemFor(Audience.Technical);

        Assert.Contains("One line per entry, one entry per change", system);
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
    public void Asks_a_technical_entry_to_say_what_differs_and_marks_a_breaking_one()
    {
        string system = SystemFor(Audience.Technical);

        Assert.Contains("reads correctly with its heading covered", system);
        Assert.Contains("\"**Breaking:**\"", system);
        Assert.Contains("stands first in its group", system);
    }

    [Fact]
    public void Takes_the_product_theme_as_given()
    {
        // A theme is a lookup of labels. A model asked to find one would put a word
        // in the document that no fact gave it.
        string system = SystemFor(Audience.Product);

        Assert.Contains("named by the theme in brackets", system);
        Assert.DoesNotContain("Group related changes by theme", system);
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
    public void Asks_the_customer_rendering_for_a_description_under_the_label_it_is_read_back_by()
    {
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature: dark mode"]), Audience.Customer);

        // The prompt and the parser share one constant; asserting the constant is
        // what stops a reworded prompt from silently losing the field.
        Assert.Contains(ReleaseDescription.Label, prompt.System, StringComparison.Ordinal);
        Assert.Contains("what this release is about", prompt.System, StringComparison.Ordinal);
        Assert.Contains("leave the line out entirely", prompt.System, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Audience.Technical)]
    [InlineData(Audience.Product)]
    public void No_other_audience_is_asked_for_a_description(Audience audience)
    {
        // Nothing reads one back for them, so asking would put a line in the text
        // that no output takes out again.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(new GroundedFacts(["Feature: dark mode"]), audience);

        Assert.DoesNotContain(ReleaseDescription.Label, prompt.System, StringComparison.Ordinal);
    }
}
