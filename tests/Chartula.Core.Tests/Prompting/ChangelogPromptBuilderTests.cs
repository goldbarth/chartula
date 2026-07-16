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
}
