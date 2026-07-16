using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Llm;

public sealed class ChatModelTests
{
    [Fact]
    public void Constructor_rejects_a_null_chat_client()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatModel(null!));
    }

    [Fact]
    public async Task RephraseAsync_routes_through_the_chat_client_and_returns_its_text()
    {
        StubChatClient chat = new("Signing in with an expired token now fails cleanly.");
        // The pipeline only ever sees the interface, never ChatModel or a provider.
        IChangelogModel model = new ChatModel(chat);

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
        IChangelogModel model = new ChatModel(chat);

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
        IChangelogModel model = new ChatModel(chat);

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
        IChangelogModel model = new ChatModel(chat);

        FaithfulnessReport report = await model.CheckFaithfulnessAsync(
            new FaithfulnessRequest(
                "Fixed a parser bug.",
                new GroundedFacts(["Fixed a bug in the parser"])));

        Assert.True(report.IsFaithful);
        Assert.Empty(report.UnsupportedClaims);
    }
}
