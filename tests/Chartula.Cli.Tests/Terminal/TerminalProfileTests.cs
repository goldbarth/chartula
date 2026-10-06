using System.Collections;
using System.Text;
using Chartula.Cli.Terminal;

namespace Chartula.Cli.Tests.Terminal;

/// <summary>
/// #339: what a stream may show is decided conservatively, because a control character in
/// a log or a file is worse than a plain line in a terminal that could have shown more.
/// </summary>
public sealed class TerminalProfileTests
{
    private static TerminalProfile Detect(
        bool redirected = false, bool plain = false, Encoding? encoding = null, params (string Name, string Value)[] environment)
    {
        Hashtable variables = [];
        foreach ((string name, string value) in environment)
        {
            variables[name] = value;
        }

        return TerminalProfile.Detect(redirected, variables, plain, encoding ?? Encoding.UTF8);
    }

    [Fact]
    public void A_terminal_with_truecolor_gets_motion_colour_and_symbols()
        => Assert.Equal(
            new TerminalProfile(Live: true, ColorDepth.TrueColor, Unicode: true),
            Detect(environment: [("COLORTERM", "truecolor"), ("TERM", "xterm-256color")]));

    [Fact]
    public void Without_colorterm_the_colours_come_from_term()
    {
        Assert.Equal(ColorDepth.Ansi256, Detect(environment: [("TERM", "xterm-256color")]).Colors);
        Assert.Equal(ColorDepth.Basic, Detect(environment: [("TERM", "xterm")]).Colors);
    }

    [Fact]
    public void No_color_keeps_the_motion_and_drops_the_colour()
        => Assert.Equal(
            new TerminalProfile(Live: true, ColorDepth.None, Unicode: true),
            Detect(environment: [("NO_COLOR", "1"), ("COLORTERM", "truecolor")]));

    [Fact]
    public void A_redirected_stream_is_plain()
        => Assert.Equal(TerminalProfile.Plain, Detect(redirected: true, environment: [("COLORTERM", "truecolor")]));

    [Fact]
    public void The_plain_flag_is_plain()
        => Assert.Equal(TerminalProfile.Plain, Detect(plain: true, environment: [("COLORTERM", "truecolor")]));

    [Fact]
    public void A_dumb_terminal_is_plain()
        => Assert.Equal(TerminalProfile.Plain, Detect(environment: [("TERM", "dumb")]));

    // A CI runner may imitate a terminal, and its log keeps every control character.
    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Ci_is_plain_unless_it_says_it_is_not_ci(string value, bool plain)
        => Assert.Equal(plain, Detect(environment: [("CI", value)]) == TerminalProfile.Plain);

    [Fact]
    public void A_console_that_cannot_carry_unicode_gets_ascii()
        => Assert.False(Detect(encoding: Encoding.Latin1).Unicode);
}
