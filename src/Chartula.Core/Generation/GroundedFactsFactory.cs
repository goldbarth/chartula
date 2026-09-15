using System.Text;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Turns a fact base into the grounded fact statements fed to generation, selected
/// and ordered for the audience and category settings. Pure and deterministic; the
/// same base feeds every audience, so the renderings cannot contradict each other.
/// <para>
/// Whatever an audience's template decides rather than words is decided here and
/// handed to the model as a fact, never left to the prompt: which changes reach the
/// rendering, the group of a technical entry and its reference, the theme of a
/// product entry. The templates are in <c>docs/output-format.md</c> of
/// goldbarth/chartula-evals.
/// </para>
/// </summary>
public static class GroundedFactsFactory
{
    // Common Changelog's order: what a reader already depends on before what is new
    // to them. Its Removed group is not here because nothing in the fact base marks
    // a change as a removal, and a group with no source is never assigned.
    private static readonly string[] TechnicalGroupOrder = ["Changed", "Added", "Fixed"];

    // A theme comes from an allow-listed label. No allowlist exists yet, and the
    // format says that with none every entry stands under this one theme rather
    // than under an invented taxonomy.
    private const string ProductFallbackTheme = "Other";

    public static GroundedFacts Build(FactBase factBase, Audience audience, CategorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(factBase);
        ArgumentNullException.ThrowIfNull(settings);

        IEnumerable<ChangeFact> selected = factBase.Changes.Where(change => Reaches(change, audience));

        // Technical: by group, breaking first within it, as the format fixes both.
        // Every other audience: breaking first (when prominent), then category order.
        IEnumerable<ChangeFact> ordered = audience == Audience.Technical
            ? selected
                .OrderBy(change => Array.IndexOf(TechnicalGroupOrder, TechnicalGroup(change.Category)))
                .ThenBy(change => change.IsBreaking ? 0 : 1)
                .ThenBy(change => settings.RankOf(change.Category))
            : selected
                .OrderBy(change => settings.BreakingProminent && change.IsBreaking ? 0 : 1)
                .ThenBy(change => settings.RankOf(change.Category));

        List<string> statements = [];
        foreach (ChangeFact change in ordered)
        {
            StringBuilder statement = new();
            if (Group(change, audience) is { } group)
            {
                statement.Append('[').Append(group).Append("] ");
            }

            statement.Append(settings.DisplayName(change.Category));
            if (change.IsBreaking)
            {
                statement.Append(" (breaking)");
            }

            statement.Append(": ").Append(change.Title);
            if (!string.IsNullOrEmpty(change.Description))
            {
                statement.Append(" - ").Append(change.Description);
            }

            if (audience == Audience.Technical && Reference(change) is { } reference)
            {
                statement.Append(' ').Append(reference);
            }

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }

    // The customer rendering follows visibility, labels included. The technical and
    // product renderings follow the categorical default, which a label can widen but
    // not narrow: an internal label says a user cannot meet a change, not that a
    // developer or a product manager cannot.
    private static bool Reaches(ChangeFact change, Audience audience) => audience switch
    {
        Audience.Customer => change.IsUserVisible,
        Audience.Technical or Audience.Product =>
            change.IsUserVisible || FactBaseBuilder.IsOutwardFacingCategory(change.Category),
        _ => true,
    };

    private static string? Group(ChangeFact change, Audience audience) => audience switch
    {
        Audience.Technical => TechnicalGroup(change.Category),
        Audience.Product => ProductFallbackTheme,
        _ => null,
    };

    // New functionality is Added and a repair is Fixed. Everything else that reaches
    // this reader changed what already existed.
    private static string TechnicalGroup(ChangeCategory category) => category switch
    {
        ChangeCategory.Feature => "Added",
        ChangeCategory.Fix => "Fixed",
        _ => "Changed",
    };

    // The reference is written here, whole, so the model copies a link rather than
    // builds one. A commit-based change has no pull request and gets no reference.
    private static string? Reference(ChangeFact change)
        => change.Url is null
            ? null
            : change.Number is { } number
                ? $"([#{number}]({change.Url}))"
                : $"({change.Url})";
}
