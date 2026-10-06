using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Chartula.Core.Prompting;

/// <summary>
/// Puts a change's title and description into a fact statement as quoted data (#337).
/// Both are written by whoever opened the pull request, so on a repository that takes
/// outside contributions they may hold text meant to steer the model. Between tags the
/// text cannot close, the model can tell where the author's words end and Chartula's
/// instructions begin, and the system prompts say to treat them as data.
/// <para>
/// A description is cut at <see cref="DescriptionLimit"/> characters here, in the prompt,
/// not in the fact base: the run record keeps it whole, and a fact dropped from the fact
/// base could not be recovered downstream. The rephrasing and the thorough check read the
/// same cut text, so the check never asks for what the model was not shown.
/// </para>
/// </summary>
public static partial class PullRequestQuote
{
    /// <summary>
    /// The characters of a description the model reads. Detailed pull request bodies in
    /// this repository run to about 4,300; the limit leaves room for those and stops a
    /// pasted log from being sent in full, once per audience and once per check.
    /// </summary>
    public const int DescriptionLimit = 8000;

    // An opening or closing tag of the two names, in any case, so quoted text cannot end
    // its own quote early or open a second one.
    [GeneratedRegex(@"<(?=/?\s*(title|description)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Tag();

    public static void Append(StringBuilder statement, string title, string? description)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(title);

        statement.Append("<title>").Append(Neutralize(title)).Append("</title>");
        if (!string.IsNullOrEmpty(description))
        {
            statement.Append(" <description>").Append(Neutralize(Cut(description))).Append("</description>");
        }
    }

    private static string Cut(string description)
    {
        if (description.Length <= DescriptionLimit)
        {
            return description;
        }

        // Never split a surrogate pair: half of one is not a character.
        int end = char.IsHighSurrogate(description[DescriptionLimit - 1]) ? DescriptionLimit - 1 : DescriptionLimit;
        string rest = (description.Length - end).ToString("N0", CultureInfo.InvariantCulture);
        return $"{description[..end]}\n[cut: {rest} more characters not shown]";
    }

    private static string Neutralize(string text) => Tag().Replace(text, "&lt;");
}
