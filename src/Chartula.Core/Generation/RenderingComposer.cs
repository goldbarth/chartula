using System.Text;
using System.Text.RegularExpressions;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Puts a rendering together from its plan and the texts the model wrote. Headings,
/// order, markers and references all come from the plan, so the structure of a
/// rendering is the same whichever model wrote its words - issue #96.
/// </summary>
public static partial class RenderingComposer
{
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>
    /// What is wrong with an answer that does not map onto the plan one to one, or
    /// <c>null</c> when every planned entry has exactly one text. A fact left without
    /// an entry would be dropped from the rendering without a trace, so it is named
    /// rather than skipped.
    /// </summary>
    public static string? FindMismatch(RenderPlan plan, RenderedEntries rendered)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rendered);

        HashSet<int> planned = [.. plan.Entries.Select(entry => entry.Id)];
        List<RenderedEntry> written = [.. rendered.Entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Text))];

        List<int> missing = [.. planned.Where(id => written.All(entry => entry.Id != id))];
        List<int> duplicate = [.. written.GroupBy(entry => entry.Id).Where(g => g.Count() > 1).Select(g => g.Key)];
        List<int> unknown = [.. written.Select(entry => entry.Id).Where(id => !planned.Contains(id)).Distinct()];

        List<string> problems = [];
        if (missing.Count > 0)
        {
            problems.Add($"no entry for fact {string.Join(", ", missing)}");
        }

        if (duplicate.Count > 0)
        {
            problems.Add($"more than one entry for fact {string.Join(", ", duplicate)}");
        }

        if (unknown.Count > 0)
        {
            problems.Add($"an entry for fact {string.Join(", ", unknown)}, which it was not sent");
        }

        return problems.Count == 0
            ? null
            : "the model's answer does not match the facts it was sent: " + string.Join("; ", problems);
    }

    /// <summary>
    /// The rendering as Markdown. Expects an answer <see cref="FindMismatch"/> accepts.
    /// </summary>
    public static string Compose(RenderPlan plan, RenderedEntries rendered, Audience audience)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rendered);

        Dictionary<int, RenderedEntry> byId = rendered.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
            .ToDictionary(entry => entry.Id);

        StringBuilder text = new();
        string? group = null;
        foreach (PlannedEntry planned in plan.Entries)
        {
            if (planned.Group != group)
            {
                if (text.Length > 0)
                {
                    text.Append("\n\n");
                }

                text.Append("### ").Append(planned.Group).Append("\n\n");
                group = planned.Group;
            }
            else
            {
                text.Append('\n');
            }

            text.Append("- ").Append(Line(planned, byId[planned.Id], audience));
        }

        return text.ToString();
    }

    /// <summary>A text the model wrote, as one line, or <c>null</c> when there is none.</summary>
    public static string? SingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string line = Whitespace().Replace(value, " ").Trim();

        // A model that writes the bullet itself is still answering the question asked.
        return line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)
            ? line[2..].TrimStart()
            : line;
    }

    private static string Line(PlannedEntry planned, RenderedEntry entry, Audience audience)
    {
        string body = SingleLine(entry.Text)!;
        const string Breaking = "**Breaking:** ";

        switch (audience)
        {
            case Audience.Technical:
                string reference = planned.Reference is null ? string.Empty : " " + planned.Reference;
                return (planned.IsBreaking ? Breaking : string.Empty) + body + reference;

            case Audience.Customer:
                // The template gives a breaking change its marker as the label.
                if (planned.IsBreaking)
                {
                    return Breaking + body;
                }

                string? label = SingleLine(entry.Label)?.Trim('*', ' ').TrimEnd(':').Trim();
                return string.IsNullOrEmpty(label) ? body : $"**{label}:** {body}";

            default:
                return body;
        }
    }
}
