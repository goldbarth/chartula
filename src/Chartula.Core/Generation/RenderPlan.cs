using Chartula.Core.Facts;

namespace Chartula.Core.Generation;

/// <summary>
/// Everything about one audience's rendering that is decided before the model is
/// called: the facts it is sent, and for each of them the group it stands in, its
/// place in the order, and what code puts around its text.
/// </summary>
/// <param name="Facts">The fact statements the model rephrases, each opening on its id.</param>
/// <param name="Entries">One planned entry per fact, in the order the rendering shows them.</param>
public sealed record RenderPlan(GroundedFacts Facts, IReadOnlyList<PlannedEntry> Entries);
