using System.Text.RegularExpressions;

namespace Chartula.Core.Curation;

/// <summary>
/// What of a pull request body counts as the author's description. Two things in a
/// body are not, although they look like text:
/// <list type="bullet">
/// <item>HTML comments. GitHub does not render them, so nobody reading the pull
/// request saw them; a template's placeholders are comments, and so are its
/// examples - "Closes #123", a breaking-change hint - which read as facts would link
/// an issue nobody named and flag a change nobody called breaking.</item>
/// <item>A body with nothing left but headings and checklist items. That is a
/// template nobody filled in, an absent source that looks present, and an absent
/// source produces an absent field.</item>
/// </list>
/// A body with anything else in it is kept as written, headings and boxes included:
/// curation removes what the author did not say, it does not edit what they did.
/// </summary>
internal static partial class PullRequestBody
{
    // An unterminated comment runs to the end, as GitHub renders it: hidden.
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
