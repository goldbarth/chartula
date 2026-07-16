using Chartula.Core.Categorization;

namespace Chartula.Core.Labeling;

/// <summary>
/// The outcome of applying label rules to a change: whether it is included, and a
/// category a label forces it into (overriding the deterministic categorization).
/// </summary>
/// <param name="Include">Whether the change stays in the changelog.</param>
/// <param name="ForcedCategory">
/// A category imposed by a label, or <c>null</c> when no label forces one.
/// </param>
public sealed record LabelDecision(bool Include, ChangeCategory? ForcedCategory);
