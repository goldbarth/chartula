using Chartula.Core.Llm;

namespace Chartula.Cli.Commands;

/// <summary>
/// Reads <c>--audience</c> into the set of audiences a run renders.
/// <para>
/// Absent means all of them, which is what a release wants. Naming fewer is for
/// a run that measures one audience's wording: each audience is its own
/// rephrasing call and its own faithfulness check, so a run that asks for one
/// pays for one.
/// </para>
/// </summary>
internal static class AudienceSelection
{
    /// <summary>
    /// The audiences named on the command line, or <c>null</c> when none were.
    /// Returns false and fills <paramref name="error"/> for a name that is not an
    /// audience - a misspelling that rendered nothing would otherwise look like a
    /// release with nothing to say.
    /// </summary>
    public static bool TryParse(
        IReadOnlyList<string> args,
        out IReadOnlyCollection<Audience>? audiences,
        out string? error)
    {
        audiences = null;
        error = null;

        IReadOnlyList<string> named = CommandLineArguments.GetOptions(args, "--audience");
        if (named.Count == 0)
        {
            return true;
        }

        List<Audience> parsed = [];
        foreach (string name in named)
        {
            if (!Enum.TryParse(name, ignoreCase: true, out Audience audience)
                || !Enum.IsDefined(audience))
            {
                error = $"Unknown audience '{name}'. There are three: "
                        + string.Join(", ", Enum.GetNames<Audience>().Select(n => n.ToLowerInvariant()))
                        + ".";
                return false;
            }

            if (!parsed.Contains(audience))
            {
                parsed.Add(audience);
            }
        }

        audiences = parsed;
        return true;
    }
}
