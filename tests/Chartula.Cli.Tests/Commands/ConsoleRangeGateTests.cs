using Chartula.Cli.Commands;
using Chartula.Core.History;
using Chartula.Core.Pipeline;

namespace Chartula.Cli.Tests.Commands;

/// <summary>
/// The header names both ends of the range, and a large range is confirmed twice, no
/// by default. Without a terminal nobody can answer, so the gate declines and names <c>--yes</c>.
/// </summary>
public sealed class ConsoleRangeGateTests
{
    private static readonly CommitRange FirstTag = Range(from: null, commits: 100);

    private static CommitRange Range(string? from, int commits)
        => new("v0.1.0", from, [.. Enumerable.Range(0, commits).Select(static n => new CommitInfo($"sha{n}", "feat: A"))]);

    private static (ConsoleRangeGate Gate, StringWriter Output) Gate(string answers, bool interactive = true)
    {
        StringWriter output = new();
        return (new ConsoleRangeGate(new StringReader(answers), output, interactive, new LargeRangeRule(200)), output);
    }

    [Fact]
    public void The_header_names_a_first_tag_as_every_commit_up_to_it()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("");

        gate.Announce(FirstTag);

        Assert.Equal("Range:  every commit up to v0.1.0 (100 commits), the first tag", output.ToString().TrimEnd());
    }

    [Fact]
    public void The_header_names_both_ends_of_a_bounded_range()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("");

        gate.Announce(Range(from: "v0.0.9", commits: 1));

        Assert.Equal("Range:  the commits after v0.0.9, up to v0.1.0 (1 commit)", output.ToString().TrimEnd());
    }

    [Fact]
    public async Task Two_yeses_confirm()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("y\nyes\n");

        Assert.True(await gate.ConfirmAsync(FirstTag));
        Assert.Contains("It costs 100 GitHub requests", output.ToString());
        Assert.Contains("--since <ref>: the commits after <ref>, up to v0.1.0", output.ToString());
        Assert.Contains("Render all 100 commits up to v0.1.0? [y/N]", output.ToString());
        Assert.Contains("Continue? [y/N]", output.ToString());
    }

    [Theory]
    [InlineData("y\nn\n")]
    [InlineData("n\n")]
    [InlineData("\n\n")]
    [InlineData("")]
    public async Task Anything_but_two_yeses_declines(string answers)
    {
        (ConsoleRangeGate gate, _) = Gate(answers);

        Assert.False(await gate.ConfirmAsync(FirstTag));
    }

    [Fact]
    public async Task A_declined_first_answer_is_not_followed_by_the_second_question()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("n\ny\n");

        Assert.False(await gate.ConfirmAsync(FirstTag));
        Assert.DoesNotContain("Continue?", output.ToString());
    }

    [Fact]
    public async Task Without_a_terminal_it_declines_and_names_yes()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("y\ny\n", interactive: false);

        Assert.False(await gate.ConfirmAsync(FirstTag));
        Assert.Contains("No terminal to confirm this. Pass --yes to confirm it up front.", output.ToString());
        Assert.DoesNotContain("[y/N]", output.ToString());
    }

    [Fact]
    public async Task A_bounded_range_says_it_is_above_the_threshold()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("n\n");

        await gate.ConfirmAsync(Range(from: "v0.0.9", commits: 350));

        Assert.Contains("That is more than range.confirmAboveCommits (200).", output.ToString());
    }

    [Fact]
    public async Task A_first_tag_does_not_claim_to_be_above_the_threshold()
    {
        (ConsoleRangeGate gate, StringWriter output) = Gate("n\n");

        await gate.ConfirmAsync(Range(from: null, commits: 2));

        Assert.DoesNotContain("confirmAboveCommits", output.ToString());
    }
}
