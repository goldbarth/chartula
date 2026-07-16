using System.Text;

namespace Chartula.Core.Serialization;

/// <summary>
/// Composes the new <c>CHANGELOG.md</c> content from the existing file and a
/// release. The new section is prepended at the top; existing sections are kept
/// verbatim. Running twice for the same tag replaces that section in place rather
/// than duplicating it, so the operation is idempotent and never reorders history.
/// Pure and deterministic; the file I/O lives in the writer.
/// </summary>
public static class ChangelogMarkdownComposer
{
    private const string DefaultHeader = "# Changelog";

    public static string Compose(string? existingContent, string tag, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(body);

        bool isNew = string.IsNullOrWhiteSpace(existingContent);
        string normalized = Normalize(existingContent ?? string.Empty);

        (string header, List<Section> sections) = Parse(normalized, isNew);

        Section newSection = new(tag, BuildSection(tag, Normalize(body).Trim()));
        int existing = sections.FindIndex(section => string.Equals(section.Tag, tag, StringComparison.Ordinal));
        if (existing >= 0)
        {
            sections[existing] = newSection; // replace in place: no duplicate, order kept
        }
        else
        {
            sections.Insert(0, newSection); // prepend the newest release
        }

        return Assemble(header, sections);
    }

    private sealed record Section(string Tag, string Raw);

    private static (string Header, List<Section> Sections) Parse(string content, bool isNew)
    {
        string[] lines = content.Split('\n');

        List<string> headerLines = [];
        int i = 0;
        for (; i < lines.Length && !IsSectionHeading(lines[i], out _); i++)
        {
            headerLines.Add(lines[i]);
        }

        List<Section> sections = [];
        while (i < lines.Length)
        {
            IsSectionHeading(lines[i], out string tag);
            List<string> block = [lines[i]];
            i++;
            for (; i < lines.Length && !IsSectionHeading(lines[i], out _); i++)
            {
                block.Add(lines[i]);
            }

            sections.Add(new Section(tag, string.Join('\n', block).Trim()));
        }

        string header = string.Join('\n', headerLines).Trim();
        if (header.Length == 0 && isNew)
        {
            header = DefaultHeader;
        }

        return (header, sections);
    }

    private static bool IsSectionHeading(string line, out string tag)
    {
        tag = string.Empty;
        if (!line.StartsWith("## ", StringComparison.Ordinal))
        {
            return false;
        }

        string rest = line[3..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        int space = rest.IndexOfAny([' ', '\t']);
        tag = space < 0 ? rest : rest[..space];
        return true;
    }

    private static string BuildSection(string tag, string body)
        => body.Length == 0 ? $"## {tag}" : $"## {tag}\n\n{body}";

    private static string Assemble(string header, List<Section> sections)
    {
        StringBuilder builder = new();
        if (header.Length > 0)
        {
            builder.Append(header);
        }

        foreach (Section section in sections)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(section.Raw);
        }

        builder.Append('\n');
        return builder.ToString();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
