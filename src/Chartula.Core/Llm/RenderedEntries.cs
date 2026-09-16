namespace Chartula.Core.Llm;

/// <summary>
/// The shape the model fills in when it rephrases a release, and the schema its
/// response is held to: one text per fact, keyed by the id the fact was sent with.
/// </summary>
/// <remarks>
/// Only words travel back. Headings, groups, order, references and markers are
/// decided before the call and put around these texts in code, so no model decides
/// the structure of a rendering. Issue #96 is why: five renderings of one release on
/// four models came back in five structures, including two of the same model.
/// </remarks>
/// <param name="Entries">One entry per fact the model was sent.</param>
/// <param name="Description">
/// A one-sentence summary of the release. Asked of the customer audience only, and
/// empty when the facts do not support one.
/// </param>
public sealed record RenderedEntries(IReadOnlyList<RenderedEntry> Entries, string? Description = null);
