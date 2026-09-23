namespace Chartula.Core.Formatting;

/// <summary>
/// Default <see cref="IChangelogFormatter"/>.
/// It applies conservative rules that keep the structure, so every rendering is
/// internally consistent:
/// <list type="bullet">
/// <item>normalized line endings,</item>
/// <item>a single bullet marker,</item>
/// <item>no trailing whitespace,</item>
/// <item>no leading, trailing or repeated blank lines.</item>
/// </list>
/// Lines that are not bullets (headings, prose) are left intact.
/// </summary>
public sealed class ChangelogFormatter : IChangelogFormatter
{
    // Markers rewritten to a "- " bullet. A leading hyphen is already the target
    // marker, so only its spacing is normalized.
    private static readonly char[] BulletMarkers = ['*', '+', '•']; // '•'

    public string Format(string rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        string[] rawLines = rendered.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        List<string> lines = [];
        bool lastWasBlank = false;
        foreach (string raw in rawLines)
        {
            string line = NormalizeLine(raw);
            bool blank = line.Length == 0;

            // Drop leading blanks and collapse runs of blank lines.
            if (blank && (lines.Count == 0 || lastWasBlank))
            {
                continue;
            }

            lines.Add(line);
            lastWasBlank = blank;
        }

        // Drop trailing blanks.
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    private static string NormalizeLine(string raw)
    {
        string trimmedEnd = raw.TrimEnd();
        string trimmed = trimmedEnd.TrimStart();

        if (trimmed.Length >= 2 && trimmed[1] == ' '
            && (Array.IndexOf(BulletMarkers, trimmed[0]) >= 0 || trimmed[0] == '-'))
        {
            return "- " + trimmed[2..].TrimStart();
        }

        // Not a bullet: keep the line (headings, prose), trailing space removed.
        return trimmedEnd;
    }
}
