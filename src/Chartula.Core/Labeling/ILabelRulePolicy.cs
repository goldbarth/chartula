using Chartula.Core.Curation;

namespace Chartula.Core.Labeling;

/// <summary>
/// Applies <see cref="LabelRules"/> to a change to decide inclusion and any
/// label-forced category. With <see cref="LabelRules.None"/> every change is
/// included and no category is forced.
/// </summary>
public interface ILabelRulePolicy
{
    LabelDecision Evaluate(ReleaseChange change);
}
