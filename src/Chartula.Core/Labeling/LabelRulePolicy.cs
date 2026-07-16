using Chartula.Core.Categorization;
using Chartula.Core.Curation;

namespace Chartula.Core.Labeling;

/// <summary>
/// Default <see cref="ILabelRulePolicy"/>. Exclusion wins over everything; then
/// the only-labeled mode drops unlabeled changes; otherwise the change is
/// included, with the first matching label deciding any forced category.
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

        return new LabelDecision(Include: true, forced);
    }
}
