using System.Text.RegularExpressions;
using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Filtering;
using Chartula.Core.History;
using Chartula.Core.Labeling;
using Chartula.Core.PullRequests;

namespace Chartula.Core.Facts;

/// <summary>
/// Default <see cref="IFactBaseBuilder"/>.
/// It resolves changes (with the missing-PR fallback), drops filtered-out changes
/// and maps each remaining change to a <see cref="ChangeFact"/>.
/// Category and flags come from the deterministic curation steps, never from an LLM.
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
            IsUserVisible: IsUserVisible(category, classification.IsBreaking, label.UserVisible),
            IsBreaking: classification.IsBreaking,
            LinkedIssues: linkedIssues,
            // Keep labels verbatim and unfiltered. The rendering decides which labels
            // it shows, and a fact the fact base drops cannot be recovered downstream.
            Labels: change.Labels,
            Description: string.IsNullOrEmpty(description) ? null : description);
    }

    // A breaking change is always user-visible, whatever a label says.
    // Otherwise a visibility label decides: the PR author knows whether users meet the change.
    // A category cannot tell, because a feature can be entirely internal.
    // Without a label the category decides, which is the default for repositories without visibility labels.
    private static bool IsUserVisible(ChangeCategory category, bool isBreaking, bool? labelled)
        => isBreaking || (labelled ?? IsOutwardFacingCategory(category));

    // The fallback when no visibility label decides: outward-facing categories only.
    // Refactors, internal work and docs are left out.
    // The technical and product renderings also call this directly: a visibility
    // label may widen what reaches them, but not narrow it.
    internal static bool IsOutwardFacingCategory(ChangeCategory category)
        => category is ChangeCategory.Feature
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
