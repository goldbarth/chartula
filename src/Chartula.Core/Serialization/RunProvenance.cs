namespace Chartula.Core.Serialization;

/// <summary>
/// How a run was made, stored with its facts so a release can be explained from
/// the artifact after the terminal output is gone. Only what identifies the run's
/// inputs: an endpoint host, flags or check verdicts stay out, because the file is
/// public and a host can name an internal gateway. A value the run does not have
/// is <c>null</c> and left out of the file, never written empty.
/// </summary>
/// <param name="ToolVersion">The Chartula version that wrote the file.</param>
/// <param name="Provider">The model provider, as configured: <c>anthropic</c> or <c>openai-compatible</c>.</param>
/// <param name="Model">The model id the renderings were written with.</param>
/// <param name="PromptHash">
/// A hash of the prompt text Chartula sends, without the facts - see
/// <c>ChangelogPromptBuilder.PromptHash</c>. Two files with the same hash were
/// rendered from the same instructions.
/// </param>
public sealed record RunProvenance(string? ToolVersion, string? Provider, string? Model, string? PromptHash);
