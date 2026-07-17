using Chartula.Core.Facts;
using Chartula.Core.Llm;
using Chartula.Core.Prompting;

namespace Chartula.Core.Tests.Llm;

public sealed class ChatModelTests
{
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
    public async Task RephraseAsync_routes_through_the_chat_client_and_returns_its_text()
    {
        StubChatClient chat = new("Signing in with an expired token now fails cleanly.");
        // The pipeline only ever sees the interface, never ChatModel or a provider.
        IChangelogModel model = Model(chat);

        string result = await model.RephraseAsync(
            new RephraseRequest(
                new GroundedFacts(["Fixed a bug where expired tokens were accepted"]),
                Audience.Customer));

        Assert.Equal("Signing in with an expired token now fails cleanly.", result);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task RephraseAsync_feeds_the_facts_and_audience_into_the_prompt()
    {
        StubChatClient chat = new("...");
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
            """{"isFaithful":false,"unsupportedClaims":["closed a security hole"]}""");
        IChangelogModel model = Model(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest(
                "This release closed a security hole.",
                new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.False(report.IsFaithful);
        Assert.Contains("closed a security hole", report.UnsupportedClaims);
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

        Assert.True(report.IsFaithful);
        Assert.Empty(report.UnsupportedClaims);
    }

    // Providers require an output ceiling and quietly substitute a small default when
    // one is absent, which truncates a changelog mid-sentence. Every call must carry it.
    [Fact]
    public async Task RephraseAsync_sends_the_configured_output_ceiling()
    {
        StubChatClient chat = new("...");
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
        StubChatClient chat = new("...");
        IChangelogModel model = Model(chat);

        await model.RephraseAsync(
            new RephraseRequest(new GroundedFacts(["Added dark mode"]), Audience.Customer));

        Assert.Equal(new ChatModelOptions().MaxOutputTokens, chat.LastOptions?.MaxOutputTokens);
    }
}
