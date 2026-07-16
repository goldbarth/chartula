using System.Text;
using Chartula.Core.Categorization;
using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Generation;

/// <summary>
/// Turns a fact base into the grounded fact statements fed to generation, selected
/// and ordered for the audience and category settings. Customer entries are
/// user-facing only; technical entries keep the pull request link. Changes are
/// ordered by the configured category order, with breaking changes floated to the
/// top when they are shown prominently, and each carries its configured category
/// display name. Pure and deterministic; the same base feeds every audience, so
/// the renderings cannot contradict each other.
/// </summary>
public static class GroundedFactsFactory
{
    public static GroundedFacts Build(FactBase factBase, Audience audience, CategorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(factBase);
        ArgumentNullException.ThrowIfNull(settings);

        IEnumerable<ChangeFact> selected = factBase.Changes
            .Where(change => audience != Audience.Customer || change.IsUserVisible);

        // Stable order: breaking first (when prominent), then by category order.
        IEnumerable<ChangeFact> ordered = selected
            .OrderBy(change => settings.BreakingProminent && change.IsBreaking ? 0 : 1)
            .ThenBy(change => settings.RankOf(change.Category));

        List<string> statements = [];
        foreach (ChangeFact change in ordered)
        {
            StringBuilder statement = new();
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

            if (audience == Audience.Technical && !string.IsNullOrEmpty(change.Url))
            {
                statement.Append(" (").Append(change.Url).Append(')');
            }

            statements.Add(statement.ToString());
        }

        return new GroundedFacts(statements);
    }
}
