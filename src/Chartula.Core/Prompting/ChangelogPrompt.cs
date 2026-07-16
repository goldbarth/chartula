namespace Chartula.Core.Prompting;

/// <summary>
/// A built prompt: the system instructions that constrain the model, and the user
/// content that carries the established facts.
/// </summary>
/// <param name="System">The instruction prompt (rephrase only, never invent).</param>
/// <param name="User">The user prompt carrying the facts to rephrase.</param>
public sealed record ChangelogPrompt(string System, string User);
