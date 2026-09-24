using System.Text.RegularExpressions;

namespace Chartula.Core.Curation;

/// <summary>
/// Extracts the author's description from a pull request body.
/// Two parts of a body look like text but are not the author's description:
/// <list type="bullet">
/// <item>HTML comments. GitHub does not render them, so nobody reading the pull
/// request saw them. A template's placeholders and examples are comments, for example
/// "Closes #123" or a breaking-change hint. Read as facts, they would link an issue
/// nobody named and flag a change nobody called breaking.</item>
/// <item>A body with only headings and checklist items left. That is an unfilled
/// template: it looks like a source but is absent, and an absent source produces an
/// absent field.</item>
/// </list>
/// Any other body is kept as written, headings and checkboxes included.
/// Curation removes what the author did not say, but does not edit what they said.
/// </summary>
internal static partial class PullRequestBody
{
    // An unterminated comment runs to the end of the body, because GitHub hides that part too.
    [GeneratedRegex(@"<!--.*?(?:-->|\z)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    // Lines a template is made of: an ATX heading, or a task-list item.
    [GeneratedRegex(@"^\s{0,3}(?:#{1,6}(?:\s.*)?|[-*+]\s+\[[ xX]\].*)$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateLine();

    /// <summary>The description in <paramref name="body"/>, or <c>null</c> when it has none.</summary>
    public static string? Description(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        string visible = Comment().Replace(body.Replace("\r\n", "\n"), string.Empty).Trim();
        bool onlyTemplate = visible
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .All(line => TemplateLine().IsMatch(line));

        return onlyTemplate ? null : visible;
    }
}
