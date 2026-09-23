namespace Chartula.Core.Llm;

/// <summary>
/// The shape the model fills in when it rephrases a release. It is also the schema the
/// model's response must follow: one text per fact, keyed by the id the fact was sent with.
/// </summary>
/// <remarks>
/// The model returns only wording. Headings, groups, order, references and markers
/// are decided before the call, and code adds them around these texts. So no model
/// decides the structure of a rendering.
/// The reason is #96: five renderings of one release on four models came back in five
/// structures, including two from the same model.
/// </remarks>
/// <param name="Entries">One entry per fact the model was sent.</param>
/// <param name="Description">
/// A one-sentence summary of the release. Requested for the customer audience only.
/// Empty when the facts do not support one.
/// </param>
public sealed record RenderedEntries(IReadOnlyList<RenderedEntry> Entries, string? Description = null);
