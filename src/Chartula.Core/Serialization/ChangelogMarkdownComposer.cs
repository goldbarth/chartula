using System.Globalization;
using System.Text;

namespace Chartula.Core.Serialization;

/// <summary>
/// Composes the new <c>CHANGELOG.md</c> content from the existing file and a release.
/// The new section goes at the top, and existing sections are kept verbatim.
/// Running twice for the same release replaces that section in place instead of
/// duplicating it. So the operation is idempotent and never reorders history.
/// Pure and deterministic. The file I/O lives in the writer.
/// <para>
/// The section starts with <c>## VERSION - DATE</c>, Common Changelog's release
/// heading: the version without the tag's <c>v</c>, and the tag's own date.
/// Sections are matched by version. A section written as <c>## v1.2.0</c> before
/// the heading format changed still matches the same release.
/// </para>
/// </summary>
public static class ChangelogMarkdownComposer
{
    private const string DefaultHeader = "# Changelog";

    public static string Compose(string? existingContent, string tag, DateOnly? taggedAt, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(body);

        bool isNew = string.IsNullOrWhiteSpace(existingContent);
        string normalized = Normalize(existingContent ?? string.Empty);

        (string header, List<Section> sections) = Parse(normalized, isNew);

        string version = Version(tag.Trim());
        Section newSection = new(version, BuildSection(version, taggedAt, Normalize(body).Trim()));
        int existing = sections.FindIndex(section => string.Equals(section.Version, version, StringComparison.Ordinal));
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

    private sealed record Section(string Version, string Raw);

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
            IsSectionHeading(lines[i], out string version);
            List<string> block = [lines[i]];
            i++;
            for (; i < lines.Length && !IsSectionHeading(lines[i], out _); i++)
            {
                block.Add(lines[i]);
            }

            sections.Add(new Section(version, string.Join('\n', block).Trim()));
        }

        string header = string.Join('\n', headerLines).Trim();
        if (header.Length == 0 && isNew)
        {
            header = DefaultHeader;
        }

        return (header, sections);
    }

    private static bool IsSectionHeading(string line, out string version)
    {
        version = string.Empty;
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
        version = Version(space < 0 ? rest : rest[..space]);
        return true;
    }

    // "v1.2.0" is the tag, "1.2.0" the version. Only a "v" before a digit is removed,
    // so a tag that is not a version is kept as it is.
    private static string Version(string tag)
        => tag.Length > 1 && (tag[0] is 'v' or 'V') && char.IsAsciiDigit(tag[1]) ? tag[1..] : tag;

    private static string BuildSection(string version, DateOnly? taggedAt, string body)
    {
        // Leave out a date without a source instead of using today's date.
        string heading = taggedAt is { } date
            ? $"## {version} - {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : $"## {version}";
        return body.Length == 0 ? heading : $"{heading}\n\n{body}";
    }

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
