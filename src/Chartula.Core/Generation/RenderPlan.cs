using Chartula.Core.Facts;

namespace Chartula.Core.Generation;

/// <summary>
/// Everything about one audience's rendering that is decided before the model call:
/// the facts sent to the model, and for each fact its group, its position and the
/// markers and reference the code adds around its text.
/// </summary>
/// <param name="Facts">The fact statements the model rephrases, each opening on its id.</param>
/// <param name="Entries">One planned entry per fact, in the order the rendering shows them.</param>
public sealed record RenderPlan(GroundedFacts Facts, IReadOnlyList<PlannedEntry> Entries);
