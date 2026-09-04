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

        Assert.Contains("stop once the outcome is stated", system);
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
    public void Leaves_the_shape_of_an_unspecified_audience_alone(Audience audience)
    {
        // Customer is the only audience with a written specification. Inventing
        // a shape for the other two would be the same defect as leaving it to
        // the model, only harder to notice.
        ChangelogPrompt prompt = _builder.BuildRephrasePrompt(
            new GroundedFacts(["Feature: dark mode"]), audience);

        Assert.DoesNotContain("What needs action", prompt.System);
        Assert.DoesNotContain("strike the opening clause", prompt.System);
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
