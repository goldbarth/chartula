using System.Collections;
using System.Text;

namespace Chartula.Cli.Terminal;

/// <summary>How many colours a terminal shows, as far as its environment says.</summary>
internal enum ColorDepth
{
    /// <summary>No colour codes at all.</summary>
    None,

    /// <summary>The 16 ANSI colours every colour terminal has.</summary>
    Basic,

    /// <summary>The xterm 256-colour palette.</summary>
    Ansi256,

    /// <summary>24-bit colour, which the spinner's fade needs.</summary>
    TrueColor,
}

/// <summary>
/// What one output stream may show: motion, colour and Unicode decoration (#339).
/// Decided once per stream and conservatively, because a control character in a log or a
/// file is worse than a plain line in a terminal that could have shown more.
/// <para>
/// A stream that is not a terminal, CI (which may imitate one), <c>TERM=dumb</c> and
/// <c>--plain</c> get the plain profile: whole lines, no ESC, no carriage return.
/// <c>NO_COLOR</c> keeps the motion and drops only the colour, as its convention asks.
/// Truecolor is read from <c>COLORTERM</c>, never assumed from a terminal alone.
/// </para>
/// </summary>
/// <param name="Live">Whether a line may be redrawn in place.</param>
/// <param name="Colors">The colours the stream may use.</param>
/// <param name="Unicode">Whether symbols beyond ASCII show, such as the braille spinner.</param>
internal sealed record TerminalProfile(bool Live, ColorDepth Colors, bool Unicode)
{
    public const string PlainFlag = "--plain";

    /// <summary>Whole lines of ASCII text, nothing else.</summary>
    public static TerminalProfile Plain { get; } = new(Live: false, ColorDepth.None, Unicode: false);

    public static TerminalProfile Detect(bool redirected, IDictionary environment, bool plain, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(encoding);

        if (plain
            || redirected
            || string.Equals(Variable(environment, "TERM"), "dumb", StringComparison.OrdinalIgnoreCase)
            || IsCi(Variable(environment, "CI")))
        {
            return Plain;
        }

        // Only an encoding that can carry the symbols shows them; a console on another
        // code page would print the braille frames as question marks.
        bool unicode = encoding.CodePage == Encoding.UTF8.CodePage;
        return new TerminalProfile(Live: true, ColorsOf(environment), unicode);
    }

    private static ColorDepth ColorsOf(IDictionary environment)
    {
        if (!string.IsNullOrEmpty(Variable(environment, "NO_COLOR")))
        {
            return ColorDepth.None;
        }

        string? colorTerm = Variable(environment, "COLORTERM");
        if (string.Equals(colorTerm, "truecolor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(colorTerm, "24bit", StringComparison.OrdinalIgnoreCase))
        {
            return ColorDepth.TrueColor;
        }

        return Variable(environment, "TERM")?.Contains("256color", StringComparison.OrdinalIgnoreCase) == true
            ? ColorDepth.Ansi256
            : ColorDepth.Basic;
    }

    // CI providers set CI to "true"; an explicit "false" or "0" says a job is not one.
    private static bool IsCi(string? value)
        => !string.IsNullOrEmpty(value)
           && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
           && value != "0";

    private static string? Variable(IDictionary environment, string name) => environment[name] as string;
}
