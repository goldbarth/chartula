using Chartula.Core.Categorization;

namespace Chartula.Core.Labeling;

/// <summary>
/// The outcome of applying label rules to a change:
/// <list type="bullet">
/// <item>whether the change is included,</item>
/// <item>a category a label forces, overriding the deterministic categorization,</item>
/// <item>whether the labels mark the change as user-visible.</item>
/// </list>
/// </summary>
/// <param name="Include">Whether the change stays in the changelog.</param>
/// <param name="ForcedCategory">
/// A category imposed by a label, or <c>null</c> when no label forces one.
/// </param>
/// <param name="UserVisible">
/// <c>true</c> or <c>false</c> when a label says whether users can meet the change.
/// <c>null</c> when no label says so.
/// <c>null</c> is deliberately distinct from <c>false</c>: without a label the
/// category-based fallback decides.
/// </param>
public sealed record LabelDecision(bool Include, ChangeCategory? ForcedCategory, bool? UserVisible = null);
