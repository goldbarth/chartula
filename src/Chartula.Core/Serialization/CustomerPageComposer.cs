using System.Globalization;
using System.Text;

namespace Chartula.Core.Serialization;

/// <summary>
/// Composes a <see cref="CustomerPage"/> into the published serialisation: YAML
/// front matter, then the rendered body.
/// Pure and deterministic. The file I/O lives in the writer.
/// <para>
/// Every field has a source. A field whose source is empty is left out, not emitted empty:
/// <list type="bullet">
/// <item><c>publishedAt</c> when the tag date could not be read,</item>
/// <item><c>description</c> when the model could not write one from the facts,</item>
/// <item><c>tags</c> when there are no labels.</item>
/// </list>
/// An empty field would read as a fact about the release, for example that it has
/// no subject or was never dated. An absent source means neither.
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
    /// The page title for a tag.
    /// A leading <c>v</c> is a repository convention, not part of the version the
    /// reader sees. So <c>v0.1.0</c> becomes "Release 0.1.0", as the format document specifies.
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
    /// A YAML scalar: plain when it reads back unchanged, double-quoted otherwise.
    /// A description is a sentence written by a model, so it can contain a colon or
    /// start with a character that has a meaning in YAML.
    /// Quoting only when needed keeps the common case as readable as the format
    /// document's example.
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
