using Chartula.Core.Categorization;

namespace Chartula.Core.Labeling;

/// <summary>
/// The outcome of applying label rules to a change: whether it is included, a
/// category a label forces it into (overriding the deterministic categorization),
/// and what the labels say about a reader being able to meet the change.
/// </summary>
/// <param name="Include">Whether the change stays in the changelog.</param>
/// <param name="ForcedCategory">
/// A category imposed by a label, or <c>null</c> when no label forces one.
/// </param>
/// <param name="UserVisible">
/// <c>true</c> or <c>false</c> when a label answers whether a reader can come into
/// contact with the change, and <c>null</c> when none does. The three states are the
/// point: silence is not a "no", and it leaves the answer to the fallback rather
/// than settling it.
/// </param>
public sealed record LabelDecision(bool Include, ChangeCategory? ForcedCategory, bool? UserVisible = null);
