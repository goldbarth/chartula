using Chartula.Cli.Terminal;

namespace Chartula.Cli.Tests.Terminal;

/// <summary>#339: the spinner's frames and its fade along the brand ramp.</summary>
public sealed class QuietPulseTests
{
    private static readonly TerminalProfile NoColor = new(Live: true, ColorDepth.None, Unicode: true);

    [Fact]
    public void A_frame_lasts_a_tenth_of_a_second_and_ten_frames_make_a_turn()
    {
        Assert.Equal("⠋", QuietPulse.Glyph(TimeSpan.Zero, NoColor));
        Assert.Equal("⠙", QuietPulse.Glyph(TimeSpan.FromMilliseconds(100), NoColor));
        Assert.Equal("⠏", QuietPulse.Glyph(TimeSpan.FromMilliseconds(950), NoColor));
        Assert.Equal("⠋", QuietPulse.Glyph(TimeSpan.FromSeconds(1), NoColor));
    }

    [Fact]
    public void The_fade_runs_from_amber_to_teal_in_three_seconds_and_back_in_three()
    {
        Assert.Equal((0xE0, 0xA3, 0x4B), QuietPulse.Fade(TimeSpan.Zero));
        Assert.Equal((0x2D, 0xD4, 0xBF), QuietPulse.Fade(TimeSpan.FromSeconds(3)));
        Assert.Equal((0xE0, 0xA3, 0x4B), QuietPulse.Fade(TimeSpan.FromSeconds(6)));
        Assert.Equal(QuietPulse.Fade(TimeSpan.FromSeconds(1)), QuietPulse.Fade(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Only_truecolor_fades_and_the_glyph_alone_is_coloured()
    {
        TerminalProfile trueColor = new(Live: true, ColorDepth.TrueColor, Unicode: true);

        Assert.Equal("\u001b[38;2;224;163;75m⠋\u001b[0m", QuietPulse.Glyph(TimeSpan.Zero, trueColor));
        Assert.Equal(
            "\u001b[38;5;179m⠋\u001b[0m",
            QuietPulse.Glyph(TimeSpan.Zero, new TerminalProfile(Live: true, ColorDepth.Ansi256, Unicode: true)));
        Assert.Equal(
            QuietPulse.Glyph(TimeSpan.Zero, new TerminalProfile(Live: true, ColorDepth.Ansi256, Unicode: true)).Replace("⠋", "⠙"),
            QuietPulse.Glyph(TimeSpan.FromMilliseconds(3100), new TerminalProfile(Live: true, ColorDepth.Ansi256, Unicode: true)));
    }

    [Fact]
    public void Without_unicode_the_frames_are_ascii()
        => Assert.Equal(
            ["|", "/", "-", "\\"],
            [.. Enumerable.Range(0, 4).Select(i => QuietPulse.Glyph(TimeSpan.FromMilliseconds(100 * i), new TerminalProfile(Live: true, ColorDepth.None, Unicode: false)))]);
}
