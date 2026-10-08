using System.Text;
using System.Text.RegularExpressions;

namespace Chartula.Cli.Terminal;

/// <summary>
/// Shows a rendering on a terminal as text to read, not as the Markdown source of a file
/// (#355): a group heading in bold without its <c>###</c>, an entry as a bullet with a
/// hanging indent, a label and <c>Breaking:</c> set apart by weight, code by colour, and a
/// link as its text alone.
/// <para>
/// It is not a Markdown renderer. A rendering is put together by Chartula
/// (<c>RenderingComposer</c>), so its structure is five constructs, and those are the ones
/// read here. Whatever else a model writes inside an entry stays as written, which is
/// also what a construct left open does: a lone backtick is a backtick.
/// </para>
/// <para>
/// Built in rather than handed to a pager or to a Markdown viewer on the <c>PATH</c>:
/// Chartula is one binary that starts no program but <c>git</c>, and a viewer most
/// machines do not have would make the summary depend on what is installed.
/// </para>
/// <para>
/// Only a live terminal gets this form. Plain and redirected output keep the source,
/// because there the text is copied or read by a program, and a file has no width.
/// </para>
/// </summary>
internal static partial class TerminalMarkdown
{
    private const string Reset = "\u001b[0m";
    private const string Bold = "\u001b[1m";

    // A paragraph and a heading stand two columns in, under the audience's name. An entry
    // stands two further in, and its text hangs behind its bullet.
    private const string Indent = "  ";
    private const string EntryIndent = "    ";

    // Below this an entry broken into rows is a column of single words, and the
    // terminal's own wrapping reads no worse.
    private const int NarrowestText = 20;

    private const string BreakingMarker = "Breaking:";

    [GeneratedRegex(@"\[([^\]]+)\]\([^)\s]+\)", RegexOptions.CultureInvariant)]
    private static partial Regex Link();

    private enum Style
    {
        Plain,
        Strong,
        Breaking,
        Code,
    }

    /// <summary>
    /// The rows of <paramref name="markdown"/> as a terminal shows them.
    /// </summary>
    /// <param name="markdown">A rendering, or the description of one.</param>
    /// <param name="profile">What the terminal may show.</param>
    /// <param name="columns">The terminal's width, or null to leave long rows to the terminal.</param>
    public static IReadOnlyList<string> Render(string markdown, TerminalProfile profile, int? columns)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(profile);

        List<string> rows = [];
        bool afterHeading = false;
        foreach (string source in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            string line = source.TrimEnd();
            if (line.Length == 0)
            {
                // The source separates a heading from its entries by an empty line. Here
                // the indent of the entries does that, so the line would only add height.
                if (!afterHeading && rows.Count > 0 && rows[^1].Length > 0)
                {
                    rows.Add(string.Empty);
                }

                continue;
            }

            afterHeading = line.StartsWith("### ", StringComparison.Ordinal);
            if (afterHeading)
            {
                rows.AddRange(Rows([.. line[4..].Select(static c => (c, Style.Strong))], Indent, Indent, profile, columns));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                string bullet = profile.Unicode ? "• " : "- ";
                rows.AddRange(Rows(Inline(line[2..], profile), EntryIndent + bullet, EntryIndent + "  ", profile, columns));
            }
            else
            {
                rows.AddRange(Rows(Inline(line, profile), Indent, Indent, profile, columns));
            }
        }

        while (rows.Count > 0 && rows[^1].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    // The characters of a line with the weight or colour each one is shown in. Code is
    // read first, so nothing inside a code span is taken for a link or for bold.
    private static List<(char Character, Style Style)> Inline(string text, TerminalProfile profile)
    {
        List<(char, Style)> shown = [];
        string[] parts = text.Split('`');

        // An odd number of backticks leaves the last one open: it and what follows are text.
        int paired = parts.Length % 2 == 1 ? parts.Length : parts.Length - 1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i >= paired)
            {
                Add(shown, "`" + parts[i], Style.Plain);
            }
            else if (i % 2 == 0)
            {
                Strong(shown, Link().Replace(parts[i], "$1"));
            }
            else if (profile.Colors == ColorDepth.None)
            {
                // Without colour the backticks are what still marks a name as code.
                Add(shown, $"`{parts[i]}`", Style.Plain);
            }
            else
            {
                Add(shown, parts[i], Style.Code);
            }
        }

        return shown;
    }

    private static void Strong(List<(char, Style)> shown, string text)
    {
        string[] parts = text.Split("**");
        int paired = parts.Length % 2 == 1 ? parts.Length : parts.Length - 1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i >= paired)
            {
                Add(shown, "**" + parts[i], Style.Plain);
            }
            else if (i % 2 == 0)
            {
                Add(shown, parts[i], Style.Plain);
            }
            else
            {
                Add(shown, parts[i], parts[i] == BreakingMarker ? Style.Breaking : Style.Strong);
            }
        }
    }

    private static void Add(List<(char, Style)> shown, string text, Style style)
        => shown.AddRange(text.Select(c => (c, style)));

    // Breaks between words, counting what is seen and not the codes around it, and
    // paints each row on its own, so a style never runs over a line break into the indent.
    private static IEnumerable<string> Rows(
        List<(char Character, Style Style)> text, string first, string hanging, TerminalProfile profile, int? columns)
    {
        int room = columns is { } known ? TerminalWidth.Room(known) - hanging.Length : 0;
        if (room < NarrowestText)
        {
            room = int.MaxValue;
        }

        string indent = first;
        int start = 0;
        while (start < text.Count)
        {
            int end = room >= text.Count - start ? text.Count : start + room;
            if (end < text.Count)
            {
                // Back to the last space that fits. A word longer than the room, such as
                // a URL, runs on to its own end and is not cut.
                int space = text.FindLastIndex(end, end - start + 1, static c => c.Character == ' ');
                end = space > start ? space : text.FindIndex(end, static c => c.Character == ' ');
                if (end < 0)
                {
                    end = text.Count;
                }
            }

            yield return indent + Paint(text, start, end, profile);
            indent = hanging;
            start = end;
            while (start < text.Count && text[start].Character == ' ')
            {
                start++;
            }
        }
    }

    private static string Paint(List<(char Character, Style Style)> text, int start, int end, TerminalProfile profile)
    {
        StringBuilder row = new();
        int i = start;
        while (i < end)
        {
            Style style = text[i].Style;
            int run = i;
            while (run < end && text[run].Style == style)
            {
                run++;
            }

            string piece = string.Concat(text.Skip(i).Take(run - i).Select(static c => c.Character));
            row.Append(Codes(style, profile.Colors) is { } codes ? codes + piece + Reset : piece);
            i = run;
        }

        return row.ToString();
    }

    // Weight for what names an entry, colour for what a reader types. A breaking change
    // takes the amber a warning has, where the terminal can show it.
    private static string? Codes(Style style, ColorDepth colors) => (style, colors) switch
    {
        (_, ColorDepth.None) => null,
        (Style.Strong, _) => Bold,
        (Style.Breaking, ColorDepth.TrueColor) => Bold + "\u001b[38;2;224;163;75m",
        (Style.Breaking, ColorDepth.Ansi256) => Bold + "\u001b[38;5;179m",
        (Style.Breaking, _) => Bold + "\u001b[33m",
        (Style.Code, ColorDepth.TrueColor) => "\u001b[38;2;45;212;191m",
        (Style.Code, ColorDepth.Ansi256) => "\u001b[38;5;43m",
        (Style.Code, _) => "\u001b[36m",
        _ => null,
    };
}
