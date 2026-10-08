namespace Chartula.Cli.Terminal;

/// <summary>
/// How many columns the terminal has, for the two places that write a line longer than a
/// narrow window: the step a run redraws in place, and the details of <c>doctor</c> (#349).
/// <para>
/// A line that is redrawn has to fit in one row. Once it wraps, a carriage return goes
/// back to the start of its last row only, and every frame leaves a row behind.
/// </para>
/// <para>
/// The width is not part of <see cref="TerminalProfile"/>, because a window can be resized
/// while a run works: it is read when a line is written, not once at the start.
/// </para>
/// </summary>
internal static class TerminalWidth
{
    /// <summary>
    /// The width, or null where there is none to read. The caller then writes the line as
    /// if nothing were known, which is what it did before it asked.
    /// </summary>
    public static int? Current()
    {
        try
        {
            int columns = Console.WindowWidth;
            return columns > 0 ? columns : null;
        }
        catch (Exception exception) when (exception is IOException or PlatformNotSupportedException)
        {
            // Windows throws without a console, a browser or mobile target has none.
            return null;
        }
    }

    /// <summary>
    /// The room a line may fill. The last column stays empty, because some terminals move
    /// to the next row as soon as it is written to, and others only with the next character.
    /// </summary>
    public static int Room(int columns) => columns - 1;

    /// <summary>
    /// Breaks <paramref name="text"/> between words into rows of at most
    /// <paramref name="room"/> columns. A word longer than that, such as a URL, keeps its
    /// row to itself and is not cut, so it can still be copied.
    /// </summary>
    public static IEnumerable<string> Wrap(string text, int room)
    {
        string row = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (row.Length == 0)
            {
                row = word;
            }
            else if (row.Length + 1 + word.Length <= room)
            {
                row += " " + word;
            }
            else
            {
                yield return row;
                row = word;
            }
        }

        yield return row;
    }
}
