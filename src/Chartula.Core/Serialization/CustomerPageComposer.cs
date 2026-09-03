using System.Globalization;
using System.Text;

namespace Chartula.Core.Serialization;

/// <summary>
/// Composes a <see cref="CustomerPage"/> into the published serialisation: YAML
/// front matter, then the rendered body. Pure and deterministic; the file I/O
/// lives in the writer.
/// <para>
/// Every field has a source, and a field whose source has nothing to give is left
/// out rather than emitted empty: <c>publishedAt</c> when the tag date could not
/// be read, <c>description</c> when the model could not write one from the facts,
/// <c>tags</c> when there are no labels. An empty field would read as a fact
/// about the release - that it has no subject, that it was never dated - which is
/// not what an absent source means.
/// </para>
/// </summary>
public static class CustomerPageComposer
{
    private const string Delimiter = "---";

    public static string Compose(CustomerPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(page.Tag);

        StringBuilder builder = new();
        builder.Append(Delimiter).Append('\n');

        // Field order follows the format document: title, description, publishedAt, tags.
        builder.Append("title: ").Append(Scalar(TitleFor(page.Tag))).Append('\n');

        if (!string.IsNullOrWhiteSpace(page.Description))
        {
            builder.Append("description: ").Append(Scalar(page.Description.Trim())).Append('\n');
        }

        if (page.PublishedAt is DateOnly publishedAt)
        {
            builder.Append("publishedAt: ")
                .Append(publishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append('\n');
        }

        if (page.Tags.Count > 0)
        {
            builder.Append("tags:\n");
            foreach (string tag in page.Tags)
            {
                builder.Append("  - ").Append(Scalar(tag)).Append('\n');
            }
        }

        builder.Append(Delimiter).Append('\n');

        string body = Normalize(page.Body).Trim();
        if (body.Length > 0)
        {
            builder.Append('\n').Append(body).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// The page title for a tag. A leading <c>v</c> is a repository convention and
    /// not part of the version a reader is shown, so <c>v0.1.0</c> titles a page
    /// "Release 0.1.0" - which is the shape the format document specifies.
    /// </summary>
    public static string TitleFor(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        string trimmed = tag.Trim();
        bool prefixed = trimmed.Length > 1
                        && (trimmed[0] == 'v' || trimmed[0] == 'V')
                        && char.IsAsciiDigit(trimmed[1]);

        return "Release " + (prefixed ? trimmed[1..] : trimmed);
    }

    /// <summary>
    /// A YAML scalar: written plainly where that reads back unchanged, and
    /// double-quoted where it would not. A description is a sentence written by a
    /// model, so it can hold a colon or open with a character YAML gives a meaning
    /// to; quoting only where needed keeps the common case as readable as the
    /// format document's example.
    /// </summary>
    private static string Scalar(string value)
        => NeedsQuoting(value)
            ? "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
                          .Replace("\"", "\\\"", StringComparison.Ordinal)
                          .Replace("\n", " ", StringComparison.Ordinal)
            + "\""
            : value;

    private static bool NeedsQuoting(string value)
    {
        if (value.Length == 0
            || value.Trim().Length != value.Length
            || value.Contains(": ", StringComparison.Ordinal)
            || value.Contains(" #", StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.EndsWith(':'))
        {
            return true;
        }

        const string Indicators = "-?:,[]{}#&*!|>'\"%@`";
        return Indicators.Contains(value[0]);
    }

    private static string Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
               .Replace('\r', '\n');
}
