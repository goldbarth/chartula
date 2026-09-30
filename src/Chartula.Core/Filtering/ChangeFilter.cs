using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Labeling;

namespace Chartula.Core.Filtering;

/// <summary>
/// Default <see cref="IChangeFilter"/>. It combines label rules and deterministic
/// categorization, in this order:
/// <list type="number">
///   <item>A label that excludes the change drops it, whatever else applies.</item>
///   <item>A breaking change is never dropped.</item>
///   <item>Otherwise the change is dropped when its category is excluded. A
///   label-forced category takes precedence over the deterministic one.</item>
/// </list>
/// </summary>
public sealed class ChangeFilter(
    IChangeCategorizer categorizer,
    ILabelRulePolicy labelPolicy,
    ChangeFilterRules rules) : IChangeFilter
{
    public string? DropReason(ReleaseChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        LabelDecision label = labelPolicy.Evaluate(change);
        if (!label.Include)
        {
            return label.ExcludedBecause ?? "its labels exclude it";
        }

        ChangeClassification classification = categorizer.Classify(change);
        if (classification.IsBreaking)
        {
            return null;
        }

        ChangeCategory category = label.ForcedCategory ?? classification.Category;
        return rules.ExcludedCategories.Contains(category)
            ? $"{category} is in filter.excludeCategories"
            : null;
    }

    public bool ShouldInclude(ReleaseChange change) => DropReason(change) is null;
}
