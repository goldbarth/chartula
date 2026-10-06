using Chartula.Cli.Terminal;

namespace Chartula.Cli.Tests.Terminal;

/// <summary>#339: a status reads by its word; the symbol and the colour only help the eye.</summary>
public sealed class StatusMarkTests
{
    private static TerminalProfile Profile(ColorDepth colors, bool unicode = true) => new(Live: true, colors, unicode);

    [Fact]
    public void Each_status_is_a_symbol_and_its_word_in_six_columns()
    {
        Assert.Equal("· done", StatusMarks.Format(StatusMark.Done, Profile(ColorDepth.None)));
        Assert.Equal("✓ ok  ", StatusMarks.Format(StatusMark.Ok, Profile(ColorDepth.None)));
        Assert.Equal("! warn", StatusMarks.Format(StatusMark.Warn, Profile(ColorDepth.None)));
        Assert.Equal("× fail", StatusMarks.Format(StatusMark.Fail, Profile(ColorDepth.None)));
        Assert.Equal("– skip", StatusMarks.Format(StatusMark.Skip, Profile(ColorDepth.None)));
    }

    [Fact]
    public void Without_unicode_the_symbols_are_ascii()
        => Assert.Equal(
            ". done|+ ok  |! warn|x fail|- skip",
            string.Join('|', Enum.GetValues<StatusMark>().Select(mark => StatusMarks.Format(mark, Profile(ColorDepth.None, unicode: false)))));

    [Fact]
    public void Ok_is_green_fail_red_and_warn_takes_the_amber_the_terminal_can_show()
    {
        Assert.Equal("\u001b[32m✓ ok  \u001b[0m", StatusMarks.Format(StatusMark.Ok, Profile(ColorDepth.Basic)));
        Assert.Equal("\u001b[31m× fail\u001b[0m", StatusMarks.Format(StatusMark.Fail, Profile(ColorDepth.Basic)));
        Assert.Equal("\u001b[38;2;224;163;75m! warn\u001b[0m", StatusMarks.Format(StatusMark.Warn, Profile(ColorDepth.TrueColor)));
        Assert.Equal("\u001b[38;5;179m! warn\u001b[0m", StatusMarks.Format(StatusMark.Warn, Profile(ColorDepth.Ansi256)));
        Assert.Equal("\u001b[33m! warn\u001b[0m", StatusMarks.Format(StatusMark.Warn, Profile(ColorDepth.Basic)));
    }

    // Neither a finished step nor a skipped check is a result to notice.
    [Fact]
    public void Done_and_skip_stay_neutral()
    {
        Assert.Equal("· done", StatusMarks.Format(StatusMark.Done, Profile(ColorDepth.TrueColor)));
        Assert.Equal("– skip", StatusMarks.Format(StatusMark.Skip, Profile(ColorDepth.TrueColor)));
    }
}
