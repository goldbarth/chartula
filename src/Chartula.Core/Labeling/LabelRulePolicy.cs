using Chartula.Core.Categorization;
using Chartula.Core.Curation;

namespace Chartula.Core.Labeling;

/// <summary>
/// Default <see cref="ILabelRulePolicy"/>. The rules apply in this order:
/// <list type="number">
/// <item>An excluded label drops the change, whatever else applies.</item>
/// <item>With <see cref="LabelRules.OnlyIncludeLabeled"/>, an unlabeled change is dropped.</item>
/// <item>Otherwise the change is included. The first of its labels found in
/// <see cref="LabelRules.CategoryByLabel"/> forces the category, and the visibility
/// labels decide whether users can meet the change.</item>
/// </list>
/// </summary>
public sealed class LabelRulePolicy(LabelRules rules) : ILabelRulePolicy
{
    private readonly LabelRules _rules = rules ?? throw new ArgumentNullException(nameof(rules));

    public LabelDecision Evaluate(ReleaseChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        IReadOnlyList<string> labels = change.Labels;

        if (labels.Any(_rules.ExcludedLabels.Contains))
        {
            return new LabelDecision(Include: false, ForcedCategory: null);
        }

        if (_rules.OnlyIncludeLabeled && labels.Count == 0)
        {
            return new LabelDecision(Include: false, ForcedCategory: null);
        }

        ChangeCategory? forced = null;
        foreach (string label in labels)
        {
            if (_rules.CategoryByLabel.TryGetValue(label, out ChangeCategory category))
            {
                forced = category;
                break;
            }
        }

        return new LabelDecision(Include: true, forced, UserVisible(labels));
    }

    // Returns null when no visibility label is present.
    // An internal label wins over a user-facing one. Both together are a contradiction
    // in the labels, and treating the change as internal is the safe choice: it never
    // shows an internal change to users.
    private bool? UserVisible(IReadOnlyList<string> labels)
    {
        if (labels.Any(_rules.InternalLabels.Contains))
        {
            return false;
        }

        return labels.Any(_rules.UserFacingLabels.Contains) ? true : null;
    }
}
