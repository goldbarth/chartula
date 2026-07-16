using System.Text.RegularExpressions;
using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Filtering;
using Chartula.Core.History;
using Chartula.Core.Labeling;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Facts;

/// <summary>
/// Default <see cref="IFactBaseBuilder"/>. It resolves changes (with the
/// missing-PR fallback), drops filtered-out changes, and maps each surviving
/// change to a <see cref="ChangeFact"/>. Category and flags come from the
/// deterministic curation steps, never from an LLM.
/// </summary>
public sealed partial class FactBaseBuilder(
    IReleaseChangeResolver resolver,
    IChangeFilter filter,
    IChangeCategorizer categorizer,
    ILabelRulePolicy labelPolicy,
    FactBaseDepth depth) : IFactBaseBuilder
{
    // Closing keywords that link an issue to a change (GitHub semantics).
    [GeneratedRegex(@"\b(?:close[sd]?|fixe?[sd]?|resolve[sd]?)\s+#(?<issue>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkedIssue();

    public FactBase Build(CommitRange range, IReadOnlyList<PullRequestInfo> pullRequests)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(pullRequests);

        List<ChangeFact> facts = [];
        foreach (ReleaseChange change in resolver.Resolve(range, pullRequests))
        {
            if (!filter.ShouldInclude(change))
            {
                continue;
            }

            facts.Add(ToFact(change));
        }

        return new FactBase(range.ToTag, facts);
    }

    private ChangeFact ToFact(ReleaseChange change)
    {
        ChangeClassification classification = categorizer.Classify(change);
        LabelDecision label = labelPolicy.Evaluate(change);
        ChangeCategory category = label.ForcedCategory ?? classification.Category;

        // Depth controls how much source material feeds the fact.
        string? description = depth == FactBaseDepth.TitleOnly ? null : change.Description;
        IReadOnlyList<int> linkedIssues = depth == FactBaseDepth.TitleDescriptionAndIssues
            ? ExtractLinkedIssues(change)
            : [];

        return new ChangeFact(
            Title: change.Title,
            Number: change.Number,
            Url: change.Url,
            Category: category,
            IsUserVisible: IsUserVisible(category, classification.IsBreaking),
            IsBreaking: classification.IsBreaking,
            LinkedIssues: linkedIssues,
            Description: string.IsNullOrEmpty(description) ? null : description);
    }

    // Breaking changes are always user-visible; otherwise only outward-facing
    // categories are. Refactors, internal work, and docs are not.
    private static bool IsUserVisible(ChangeCategory category, bool isBreaking)
        => isBreaking || category is ChangeCategory.Feature
            or ChangeCategory.Fix
            or ChangeCategory.Performance
            or ChangeCategory.Other;

    private static IReadOnlyList<int> ExtractLinkedIssues(ReleaseChange change)
    {
        string text = $"{change.Title}\n{change.Description}";

        List<int> issues = [];
        foreach (Match match in LinkedIssue().Matches(text))
        {
            if (int.TryParse(match.Groups["issue"].Value, out int number) && !issues.Contains(number))
            {
                issues.Add(number);
            }
        }

        return issues;
    }
}
