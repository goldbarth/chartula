using System.Text;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Turns a fact base into the <see cref="RenderPlan"/> of one audience's rendering:
/// which changes it carries, in which order, under which heading, and the fact
/// statements the model rephrases.
/// Pure and deterministic. The same fact base feeds every audience, so the renderings
/// cannot contradict each other.
/// <para>
/// Everything in an audience's template except the wording is decided here, never by
/// a model:
/// <list type="bullet">
/// <item>which changes reach the rendering,</item>
/// <item>the group and order of every entry,</item>
/// <item>a technical entry's reference,</item>
/// <item>a product entry's theme.</item>
/// </list>
/// The templates are in <c>docs/output-format.md</c> of goldbarth/chartula-evals.
/// </para>
/// </summary>
public static class GroundedFactsFactory
{
    // Common Changelog's order: changes to what the reader already depends on come
    // before what is new. Common Changelog's Removed group is missing, because nothing
    // in the fact base marks a change as a removal, and a group with no source is never assigned.
    private static readonly string[] TechnicalGroupOrder = ["Changed", "Added", "Fixed"];

    // The customer template's order: changes the reader has to act on come first,
    // above every entry that needs no action.
    private const string NeedsAction = "What needs action";
    private static readonly string[] CustomerGroupOrder = [NeedsAction, "What's New", "What's Changed", "Bug Fixes"];

    // A product theme comes from an allow-listed label. No allowlist exists yet.
    // Without one, the output format puts every entry under this single theme
    // instead of an invented taxonomy.
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

        // Technical and customer: order by group, then breaking changes first within a
        // group, as both templates require. Product has one theme, so the configured
        // category order decides.
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
                // The fourth part of a customer entry says what the reader has to do.
                // The model has to know that an action exists, not only where the entry stands.
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

    // The customer rendering follows IsUserVisible, which includes the visibility labels.
    // The technical and product renderings follow the category-based default. A label
    // can widen that default but not narrow it: an internal label says users cannot
    // meet a change, not that developers or product managers cannot.
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
    // the technical rendering changed something that already existed.
    private static string TechnicalGroup(ChangeCategory category) => category switch
    {
        ChangeCategory.Feature => "Added",
        ChangeCategory.Fix => "Fixed",
        _ => "Changed",
    };

    // A breaking change always requires action from the reader. Any other change
    // requires action only when an action-required label says so. The PR author knows
    // whether the change breaks the reader's setup; a category cannot tell.
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
