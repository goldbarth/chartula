using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;
using Microsoft.Extensions.AI;

namespace Chartula.Core.Tests.Llm;

public sealed class ChatModelTests
{
    private const string NoEntries = """{"entries":[]}""";

    private static ChatModel Model(StubChatClient chat) => new(chat, new ChangelogPromptBuilder());

    [Fact]
    public void Constructor_rejects_a_null_chat_client()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatModel(null!, new ChangelogPromptBuilder()));
    }

    [Fact]
    public void Constructor_rejects_a_null_prompt_builder()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatModel(new StubChatClient("x"), null!));
    }

    [Fact]
    public async Task RephraseAsync_routes_through_the_chat_client_and_returns_its_entries()
    {
        StubChatClient chat = new(
            """{"entries":[{"id":1,"text":"Signing in with an expired token now fails cleanly.","label":"Expired tokens"}],"description":"A security release."}""");
        // The pipeline only ever sees the interface, never ChatModel or a provider.
        IChangelogModel model = Model(chat);

        RenderedEntries result = await model.RephraseAsync(
            new RephraseRequest(
                new GroundedFacts(["[1] Fixed a bug where expired tokens were accepted"]),
                Audience.Customer));

        RenderedEntry entry = Assert.Single(result.Entries);
        Assert.Equal(1, entry.Id);
        Assert.Equal("Signing in with an expired token now fails cleanly.", entry.Text);
        Assert.Equal("Expired tokens", entry.Label);
        Assert.Equal("A security release.", result.Description);
        Assert.Equal(1, chat.CallCount);
    }

    // There is no partial rendering to fall back on, so an answer that is not entries
    // must fail loudly. The generator reports it as a failed audience.
    [Fact]
    public async Task RephraseAsync_throws_when_the_answer_is_not_entries()
    {
        StubChatClient chat = new("- Here is your changelog, as Markdown.");
        IChangelogModel model = Model(chat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => model.RephraseAsync(
            new RephraseRequest(new GroundedFacts(["[1] Added dark mode"]), Audience.Technical)));
    }

    [Fact]
    public async Task RephraseAsync_feeds_the_facts_and_audience_into_the_prompt()
    {
        StubChatClient chat = new(NoEntries);
        IChangelogModel model = Model(chat);

        await model.RephraseAsync(
            new RephraseRequest(
                new GroundedFacts(["Added dark mode"]),
                Audience.Technical));

        string prompt = string.Join("\n", chat.LastMessages!.Select(m => m.Text));
        Assert.Contains("Added dark mode", prompt);
        Assert.Contains(nameof(Audience.Technical), prompt);
    }

    [Fact]
    public async Task CheckFaithfulnessAsync_parses_the_structured_report()
    {
        StubChatClient chat = new(
            """{"isFaithful":false,"unsupportedClaims":[{"claim":"closed a security hole","reason":"the fact is a parser bug fix","pullRequest":7},{"claim":"a faster release","reason":"no fact names a speed-up"}]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest(
                "This release closed a security hole.",
                new GroundedFacts(["[#7] Fix: Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.Checked, report.Status);
        Assert.Equal(
            [
                new FaithfulnessFlag("\"closed a security hole\" - the fact is a parser bug fix", 7),
                new FaithfulnessFlag("\"a faster release\" - no fact names a speed-up"),
            ],
            report.UnsupportedClaims);
    }

    // Models quote the claim themselves more often than not.
    [Fact]
    public async Task CheckFaithfulnessAsync_does_not_quote_an_already_quoted_claim()
    {
        StubChatClient chat = new("""{"isFaithful":false,"unsupportedClaims":[{"claim":"“closed a security hole”","reason":"the fact is a parser bug fix","pullRequest":7}]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("This release closed a security hole.", new GroundedFacts(["[#7] Fix: Fixed a bug in the parser"])));

        Assert.Equal(
            new FaithfulnessFlag("\"closed a security hole\" - the fact is a parser bug fix", 7),
            Assert.Single(report.UnsupportedClaims));
    }

    // A claim without a reason still names a passage the reviewer can look at.
    [Fact]
    public async Task CheckFaithfulnessAsync_keeps_a_claim_without_a_reason()
    {
        StubChatClient chat = new("""{"isFaithful":false,"unsupportedClaims":[{"claim":"closed a security hole","reason":" ","pullRequest":7}]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("This release closed a security hole.", new GroundedFacts(["[#7] Fix: Fixed a bug in the parser"])));

        Assert.Equal(new FaithfulnessFlag("closed a security hole", 7), Assert.Single(report.UnsupportedClaims));
    }

    // A claim that names a fact but says nothing gives a reviewer nothing to read.
    // With no other claim, "unfaithful" is left without one, which is not a verdict.
    [Fact]
    public async Task CheckFaithfulnessAsync_does_not_count_a_claim_without_text()
    {
        StubChatClient chat = new("""{"isFaithful":false,"unsupportedClaims":[{"claim":" ","pullRequest":7}]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("Fixed a parser bug.", new GroundedFacts(["[#7] Fix: Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.NotEvaluated, report.Status);
        Assert.Empty(report.UnsupportedClaims);
    }

    [Fact]
    public async Task CheckFaithfulnessAsync_reports_faithful_output()
    {
        StubChatClient chat = new("""{"isFaithful":true,"unsupportedClaims":[]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest(
                "Fixed a parser bug.",
                new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.Checked, report.Status);
        Assert.Empty(report.UnsupportedClaims);
    }

    // An unreadable answer used to arrive as an empty claim list, which every caller
    // reads as "nothing found". These two tests require "nothing was checked" instead.
    [Fact]
    public async Task CheckFaithfulnessAsync_reports_an_unreadable_answer_as_not_evaluated()
    {
        StubChatClient chat = new("Sorry, I cannot produce JSON here.");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("Fixed a parser bug.", new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.NotEvaluated, report.Status);
        Assert.Empty(report.UnsupportedClaims);
        Assert.NotNull(report.Reason);
    }

    // "Unfaithful" without any claim says something is wrong without saying what.
    // Passing it on as a clean check would hide the failure.
    [Fact]
    public async Task CheckFaithfulnessAsync_reports_a_verdict_without_claims_as_not_evaluated()
    {
        StubChatClient chat = new("""{"isFaithful":false,"unsupportedClaims":[]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("Fixed a parser bug.", new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.NotEvaluated, report.Status);
    }

    // A well-formed verdict, even a clean one, is worthless if the endpoint cut the
    // prompt to fit its context window before the model saw the facts (#85, #86).
    // The characters Chartula sent set a minimum token count. A reported count below it
    // proves truncation, so the run fails outright instead of reporting a check that
    // never happened.
    [Fact]
    public async Task CheckFaithfulnessAsync_fails_when_reported_tokens_cannot_fit_the_prompt_sent()
    {
        string longOutput = new('a', 5_000);
        StubChatClient chat = new(
            """{"isFaithful":true,"unsupportedClaims":[]}""",
            new UsageDetails { InputTokenCount = 50, OutputTokenCount = 12 });
        IChangelogModel model = Model(chat);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => model.CheckFaithfulnessAsync(
                new FaithfulnessRequest(longOutput, new GroundedFacts(["Fixed a bug in the parser"]))));

        Assert.Contains("50 input tokens", exception.Message);
        Assert.Contains("context window", exception.Message);
    }

    // The same proof applies to rephrasing (#85). An endpoint that cut the facts to fit
    // its context window still answers, with entries for the facts it kept. A changelog
    // written from a third of the release then reads as if it covered all of it.
    [Fact]
    public async Task RephraseAsync_fails_when_reported_tokens_cannot_fit_the_prompt_sent()
    {
        GroundedFacts facts = new([.. Enumerable.Range(1, 600).Select(i => $"[{i}] Feature: change number {i} with a description long enough to count")]);
        StubChatClient chat = new(
            """{"entries":[{"id":1,"text":"Change one."}]}""",
            new UsageDetails { InputTokenCount = 4_096, OutputTokenCount = 12 });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Model(chat).RephraseAsync(new RephraseRequest(facts, Audience.Technical)));

        Assert.Contains("4096 input tokens", exception.Message);
        Assert.Contains("context window", exception.Message);
    }

    [Fact]
    public async Task RephraseAsync_accepts_a_reported_count_the_prompt_can_have()
    {
        StubChatClient chat = new(
            """{"entries":[{"id":1,"text":"Dark mode."}]}""",
            new UsageDetails { InputTokenCount = 900, OutputTokenCount = 12 });

        RenderedEntries entries = await Model(chat).RephraseAsync(
            new RephraseRequest(new GroundedFacts(["[1] Feature: dark mode"]), Audience.Technical));

        Assert.Single(entries.Entries);
    }

    // A provider that reports no usage gives nothing to check. That gap is real, and
    // the check passes the call instead of guessing.
    [Fact]
    public async Task CheckFaithfulnessAsync_cannot_detect_truncation_without_reported_usage()
    {
        string longOutput = new('a', 5_000);
        StubChatClient chat = new("""{"isFaithful":true,"unsupportedClaims":[]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest(longOutput, new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.Equal(FaithfulnessCheckStatus.Checked, report.Status);
    }

    // If ChatModel drops the reasoning setting, the setting silently has no effect, and
    // the only symptom is the invoice.
    [Fact]
    public async Task RephraseAsync_passes_the_reasoning_setting_through()
    {
        StubChatClient chat = new(NoEntries);
        ChatModel model = new(
            chat,
            new ChangelogPromptBuilder(),
            new ChatModelOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low } });

        await model.RephraseAsync(new RephraseRequest(new GroundedFacts(["Adds search"]), Audience.Technical));

        Assert.Equal(ReasoningEffort.Low, chat.LastOptions!.Reasoning?.Effort);
    }

    // Providers require an output limit and silently use a small default without one,
    // which cuts a changelog off mid-sentence. Every call must send it.
    [Fact]
    public async Task RephraseAsync_sends_the_configured_output_ceiling()
    {
        StubChatClient chat = new(NoEntries);
        IChangelogModel model = new ChatModel(
            chat, new ChangelogPromptBuilder(), new ChatModelOptions { MaxOutputTokens = 12_345 });

        await model.RephraseAsync(
            new RephraseRequest(new GroundedFacts(["Added dark mode"]), Audience.Customer));

        Assert.Equal(12_345, chat.LastOptions?.MaxOutputTokens);
    }

    [Fact]
    public async Task CheckFaithfulnessAsync_sends_the_configured_output_ceiling()
    {
        StubChatClient chat = new("""{"isFaithful":true,"unsupportedClaims":[]}""");
        IChangelogModel model = new ChatModel(
            chat, new ChangelogPromptBuilder(), new ChatModelOptions { MaxOutputTokens = 12_345 });

        await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest("Added dark mode.", new GroundedFacts(["Added dark mode"])));

        Assert.Equal(12_345, chat.LastOptions?.MaxOutputTokens);
    }

    [Fact]
    public async Task RephraseAsync_sends_an_output_ceiling_even_with_no_options_given()
    {
        StubChatClient chat = new(NoEntries);
        IChangelogModel model = Model(chat);

        await model.RephraseAsync(
            new RephraseRequest(new GroundedFacts(["Added dark mode"]), Audience.Customer));

        Assert.Equal(new ChatModelOptions().MaxOutputTokens, chat.LastOptions?.MaxOutputTokens);
    }

    [Fact]
    public void Leaves_the_model_room_to_write_after_it_has_finished_thinking()
    {
        // Thinking counts against the same limit, and it comes first. At 16,000, the
        // customer call spent the whole limit on thinking and was cut off before it
        // wrote a character: stop_reason max_tokens, a thinking block and no text block,
        // on four of five renders of 2026-09-04.
        Assert.True(new ChatModelOptions().MaxOutputTokens >= 32_000);
    }
}
