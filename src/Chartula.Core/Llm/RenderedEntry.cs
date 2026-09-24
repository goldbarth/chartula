namespace Chartula.Core.Llm;

/// <summary>
/// The text the model wrote for one fact.
/// </summary>
/// <param name="Id">The id of the fact this entry rephrases.</param>
/// <param name="Text">The entry itself, without any heading, marker or reference.</param>
/// <param name="Label">
/// A few words naming what the entry is about. Requested for the customer audience
/// only, where it is shown in bold in front of the text.
/// </param>
public sealed record RenderedEntry(int Id, string Text, string? Label = null);
