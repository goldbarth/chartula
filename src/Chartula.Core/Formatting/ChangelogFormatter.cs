namespace Chartula.Core.Formatting;

/// <summary>
/// Default <see cref="IChangelogFormatter"/>. It applies conservative, structure-
/// preserving rules so every rendering is internally consistent: normalized line
/// endings, a single bullet marker, trimmed trailing whitespace, and no ragged
/// blank lines. Non-bullet lines (headings, prose) are left intact.
/// </summary>
public sealed class ChangelogFormatter : IChangelogFormatter
{
    // Markers we rewrite to a "- " bullet. A leading hyphen is normalized for
    // spacing but is already the target marker.
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
