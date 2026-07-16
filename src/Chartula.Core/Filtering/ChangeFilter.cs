using Chartula.Core.Categorization;
using Chartula.Core.Curation;
using Chartula.Core.Labeling;

namespace Chartula.Core.Filtering;

/// <summary>
/// Default <see cref="IChangeFilter"/>. It combines label rules and deterministic
/// categorization:
/// <list type="number">
///   <item>a label that excludes the change wins outright;</item>
///   <item>a breaking change is never dropped;</item>
///   <item>otherwise the change is dropped when its effective category (a
///   label-forced one, else the deterministic one) is excluded.</item>
/// </list>
/// </summary>
public sealed class ChangeFilter(
    IChangeCategorizer categorizer,
    ILabelRulePolicy labelPolicy,
    ChangeFilterRules rules) : IChangeFilter
{
    public bool ShouldInclude(ReleaseChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        LabelDecision label = labelPolicy.Evaluate(change);
        if (!label.Include)
        {
            return false;
        }

        ChangeClassification classification = categorizer.Classify(change);
        if (classification.IsBreaking)
        {
            return true;
        }

        ChangeCategory category = label.ForcedCategory ?? classification.Category;
        return !rules.ExcludedCategories.Contains(category);
    }
}
