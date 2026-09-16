using System.Text;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Turns a fact base into the plan of one audience's rendering: which changes it
/// carries, in which order, under which heading, and the fact statements the model
/// rephrases. Pure and deterministic; the same base feeds every audience, so the
/// renderings cannot contradict each other.
/// <para>
/// Whatever an audience's template decides rather than words is decided here and
/// never left to a model: which changes reach the rendering, the group of every
/// entry and its order, a technical entry's reference, a product entry's theme. The
/// templates are in <c>docs/output-format.md</c> of goldbarth/chartula-evals.
/// </para>
/// </summary>
public static class GroundedFactsFactory
{
    // Common Changelog's order: what a reader already depends on before what is new
    // to them. Its Removed group is not here because nothing in the fact base marks
    // a change as a removal, and a group with no source is never assigned.
    private static readonly string[] TechnicalGroupOrder = ["Changed", "Added", "Fixed"];

    // The customer template's order: whatever the reader has to act on first, so no
    // entry that asks something stands below one that does not.
    private const string NeedsAction = "What needs action";
    private static readonly string[] CustomerGroupOrder = [NeedsAction, "What's New", "What's Changed", "Bug Fixes"];

    // A theme comes from an allow-listed label. No allowlist exists yet, and the
    // format says that with none every entry stands under this one theme rather
    // than under an invented taxonomy.
    private const string ProductFallbackTheme = "Other";

    private static readonly IReadOnlySet<string> NoLabels = new HashSet<string>();

    public static RenderPlan Build(
        FactBase factBase,
        Audience audience,
        CategorySettings settings,
        IReadOnlySet<string>? actionRequiredLabels = null)
    {
        ArgumentNullException.ThrowIfNull(factBase);
        ArgumentNullException.ThrowIfNull(settings);

        IReadOnlySet<string> actionLabels = actionRequiredLabels ?? NoLabels;
        IEnumerable<ChangeFact> selected = factBase.Changes.Where(change => Reaches(change, audience));

        // Technical and customer: by group, breaking first within it, as both
        // templates fix. Product has one theme, so the configured order decides.
        IEnumerable<ChangeFact> ordered = audience switch
        {
            Audience.Technical => selected
                .OrderBy(change => Array.IndexOf(TechnicalGroupOrder, TechnicalGroup(change.Category)))
                .ThenBy(change => change.IsBreaking ? 0 : 1)
                .ThenBy(change => settings.RankOf(change.Category)),
            Audience.Customer => selected
                .OrderBy(change => Array.IndexOf(CustomerGroupOrder, CustomerGroup(change, actionLabels)))
                .ThenBy(change => change.IsBreaking ? 0 : 1)
                .ThenBy(change => settings.RankOf(change.Category)),
            _ => selected
                .OrderBy(change => settings.BreakingProminent && change.IsBreaking ? 0 : 1)
                .ThenBy(change => settings.RankOf(change.Category)),
        };

        List<string> statements = [];
        List<PlannedEntry> entries = [];
        foreach (ChangeFact change in ordered)
        {
            int id = entries.Count + 1;

            StringBuilder statement = new();
            statement.Append('[').Append(id).Append("] ").Append(settings.DisplayName(change.Category));
            if (change.IsBreaking)
            {
                statement.Append(" (breaking)");
            }
            else if (audience == Audience.Customer && RequiresAction(change, actionLabels))
            {
                // The customer entry's fourth part is what the reader has to do, so
                // the model has to know there is something, not only where it stands.
                statement.Append(" (action required)");
            }

            statement.Append(": ").Append(change.Title);
            if (!string.IsNullOrEmpty(change.Description))
            {
                statement.Append(" - ").Append(change.Description);
            }

            statements.Add(statement.ToString());
            entries.Add(new PlannedEntry(
                id,
                Group(change, audience, actionLabels),
                change.IsBreaking,
                audience == Audience.Technical ? Reference(change) : null));
        }

        return new RenderPlan(new GroundedFacts(statements), entries);
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

    private static string Group(ChangeFact change, Audience audience, IReadOnlySet<string> actionLabels)
        => audience switch
        {
            Audience.Technical => TechnicalGroup(change.Category),
            Audience.Customer => CustomerGroup(change, actionLabels),
            _ => ProductFallbackTheme,
        };

    // New functionality is Added and a repair is Fixed. Everything else that reaches
    // this reader changed what already existed.
    private static string TechnicalGroup(ChangeCategory category) => category switch
    {
        ChangeCategory.Feature => "Added",
        ChangeCategory.Fix => "Fixed",
        _ => "Changed",
    };

    // A breaking change always asks something of the reader. Anything else asks only
    // when a label says so: whether a change costs the reader their setup is known to
    // whoever wrote it, and a category cannot tell.
    private static string CustomerGroup(ChangeFact change, IReadOnlySet<string> actionLabels)
        => RequiresAction(change, actionLabels)
            ? NeedsAction
            : change.Category switch
            {
                ChangeCategory.Feature => "What's New",
                ChangeCategory.Fix => "Bug Fixes",
                _ => "What's Changed",
            };

    private static bool RequiresAction(ChangeFact change, IReadOnlySet<string> actionLabels)
        => change.IsBreaking || change.Labels.Any(actionLabels.Contains);

    // A commit-based change has no pull request and gets no reference.
    private static string? Reference(ChangeFact change)
        => change.Url is null
            ? null
            : change.Number is { } number
                ? $"([#{number}]({change.Url}))"
                : $"({change.Url})";
}
