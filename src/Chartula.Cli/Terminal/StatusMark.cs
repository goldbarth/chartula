namespace Chartula.Cli.Terminal;

/// <summary>The states a line of output can report (#339).</summary>
internal enum StatusMark
{
    /// <summary>A step of a run ended. Says nothing about whether its result is good.</summary>
    Done,

    /// <summary>A check passed.</summary>
    Ok,

    /// <summary>A check found something to read; it does not stop a run on its own.</summary>
    Warn,

    /// <summary>A check or a step failed.</summary>
    Fail,

    /// <summary>A check was not run, for a reason given with it.</summary>
    Skip,
}

/// <summary>
/// Writes a status as a symbol and its word, such as <c>✓ ok</c>, in six columns.
/// The word carries the meaning, so it reads the same without colour and without
/// Unicode; the symbol and the colour only help the eye find it.
/// </summary>
internal static class StatusMarks
{
    private const string Reset = "\u001b[0m";
    private const string Green = "\u001b[32m";
    private const string Red = "\u001b[31m";

    public static string Format(StatusMark mark, TerminalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        string text = $"{Symbol(mark, profile.Unicode)} {Word(mark),-4}";
        return Color(mark, profile.Colors) is { } color ? color + text + Reset : text;
    }

    public static string Word(StatusMark mark) => mark switch
    {
        StatusMark.Done => "done",
        StatusMark.Ok => "ok",
        StatusMark.Warn => "warn",
        StatusMark.Fail => "fail",
        StatusMark.Skip => "skip",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

    private static string Symbol(StatusMark mark, bool unicode) => mark switch
    {
        StatusMark.Done => unicode ? "·" : ".",
        StatusMark.Ok => unicode ? "✓" : "+",
        StatusMark.Warn => "!",
        StatusMark.Fail => unicode ? "×" : "x",
        StatusMark.Skip => unicode ? "–" : "-",
        _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
    };

    // A finished step and a skipped check stay neutral: neither is a result to notice.
    // A warning takes the amber of the brand ramp where the terminal can show it.
    private static string? Color(StatusMark mark, ColorDepth colors) => (mark, colors) switch
    {
        (_, ColorDepth.None) => null,
        (StatusMark.Ok, _) => Green,
        (StatusMark.Fail, _) => Red,
        (StatusMark.Warn, ColorDepth.TrueColor) => "\u001b[38;2;224;163;75m",
        (StatusMark.Warn, ColorDepth.Ansi256) => "\u001b[38;5;179m",
        (StatusMark.Warn, _) => "\u001b[33m",
        _ => null,
    };

    /// <summary>A heading in bold where colour is allowed, and as it is elsewhere.</summary>
    public static string Heading(string text, TerminalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Colors == ColorDepth.None ? text : "\u001b[1m" + text + Reset;
    }
}
