using System.Globalization;

namespace Chartula.Cli.Terminal;

/// <summary>
/// Chartula's spinner and the colours of its status words (#339).
/// The spinner is the one thing that moves, so a step still working can be told from a
/// hanging one at a glance; its colour is decoration and never carries meaning, which the
/// status words and symbols do.
/// <para>
/// The glyph fades along the amber-to-teal ramp of the brand palette and back, three
/// seconds each way, eased at the turns so the colour does not jump. Every spinner reads
/// the same clock, so two steps in a row continue one fade instead of restarting it.
/// </para>
/// </summary>
internal static class QuietPulse
{
    /// <summary>One frame of the spinner.</summary>
    public static readonly TimeSpan FrameTime = TimeSpan.FromMilliseconds(100);

    /// <summary>How long a step runs before its spinner shows; a quicker step shows only its result.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(150);

    /// <summary>Amber to teal and back.</summary>
    private static readonly TimeSpan Cycle = TimeSpan.FromSeconds(6);

    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly string[] AsciiFrames = ["|", "/", "-", "\\"];

    // The ramp's stops on a dark background, at their positions along it.
    private static readonly (double At, int R, int G, int B)[] Ramp =
    [
        (0.00, 0xE0, 0xA3, 0x4B),
        (0.22, 0xB0, 0x7A, 0x2E),
        (0.62, 0x14, 0x80, 0x7A),
        (1.00, 0x2D, 0xD4, 0xBF),
    ];

    // The first stop in the 256-colour cube, and the nearest of the 16: one fixed accent
    // where a fade cannot be drawn without visible steps.
    private const int Amber256 = 179;
    private const int AmberBasic = 33;

    private const string Reset = "\u001b[0m";

    /// <summary>The spinner's glyph at <paramref name="elapsed"/> on the shared clock, coloured as the profile allows.</summary>
    public static string Glyph(TimeSpan elapsed, TerminalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        string[] frames = profile.Unicode ? Frames : AsciiFrames;
        string frame = frames[(int)(elapsed.Ticks / FrameTime.Ticks % frames.Length)];
        return profile.Colors switch
        {
            ColorDepth.TrueColor => Paint(TrueColor(Fade(elapsed)), frame),
            ColorDepth.Ansi256 => Paint($"\u001b[38;5;{Amber256}m", frame),
            ColorDepth.Basic => Paint($"\u001b[{AmberBasic}m", frame),
            _ => frame,
        };
    }

    /// <summary>The colour of the fade at <paramref name="elapsed"/>.</summary>
    internal static (int R, int G, int B) Fade(TimeSpan elapsed)
    {
        double phase = elapsed.Ticks % Cycle.Ticks / (double)Cycle.Ticks;
        double along = phase < 0.5 ? phase * 2 : (1 - phase) * 2;

        // Smoothstep: slow at both ends, so the turn at teal and at amber is soft.
        along = along * along * (3 - (2 * along));

        for (int i = 1; i < Ramp.Length; i++)
        {
            if (along <= Ramp[i].At)
            {
                (double fromAt, int fromR, int fromG, int fromB) = Ramp[i - 1];
                (double toAt, int toR, int toG, int toB) = Ramp[i];
                double t = (along - fromAt) / (toAt - fromAt);
                return (Mix(fromR, toR, t), Mix(fromG, toG, t), Mix(fromB, toB, t));
            }
        }

        return (Ramp[^1].R, Ramp[^1].G, Ramp[^1].B);
    }

    private static int Mix(int from, int to, double t) => (int)Math.Round(from + ((to - from) * t));

    private static string TrueColor((int R, int G, int B) color)
        => string.Create(CultureInfo.InvariantCulture, $"\u001b[38;2;{color.R};{color.G};{color.B}m");

    private static string Paint(string color, string text) => color + text + Reset;
}
