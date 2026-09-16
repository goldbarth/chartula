using System.Text.RegularExpressions;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Tests.Generation;

/// <summary>
/// Builds model answers from the facts a model was sent, so a stand-in model can
/// answer every id it received without knowing how many there were.
/// </summary>
internal static partial class Entries
{
    [GeneratedRegex(@"^\[(?<id>\d+)\]\s*(?<statement>.*)$", RegexOptions.Singleline)]
    private static partial Regex IdPrefix();

    /// <summary>Each fact's statement, without its id, as the text of its entry.</summary>
    public static RenderedEntries Echo(GroundedFacts facts, string? description = null)
        => Each(facts, (_, statement) => statement, description);

    /// <summary>One entry per fact, its text chosen by <paramref name="text"/>.</summary>
    public static RenderedEntries Each(
        GroundedFacts facts, Func<int, string, string> text, string? description = null)
        => new(
            [.. facts.Statements.Select(Parse).Select(fact => new RenderedEntry(fact.Id, text(fact.Id, fact.Statement)))],
            description);

    private static (int Id, string Statement) Parse(string statement)
    {
        Match match = IdPrefix().Match(statement);
        return match.Success
            ? (int.Parse(match.Groups["id"].Value), match.Groups["statement"].Value)
            : throw new InvalidOperationException($"A fact was sent without an id: {statement}");
    }
}
