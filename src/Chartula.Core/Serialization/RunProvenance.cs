namespace Chartula.Core.Serialization;

/// <summary>
/// How a run was made, stored with its facts, so a release can be explained from the
/// file after the terminal output is gone.
/// It holds only what identifies the run's inputs. Endpoint host, flags and check
/// verdicts stay out, because the file is public and a host can name an internal gateway.
/// A value the run does not have is <c>null</c> and left out of the file, never written empty.
/// </summary>
/// <param name="ToolVersion">The Chartula version that wrote the file.</param>
/// <param name="Provider">The model provider, as configured: <c>anthropic</c> or <c>openai-compatible</c>.</param>
/// <param name="Model">The model id the renderings were written with.</param>
/// <param name="PromptHash">
/// A hash of the prompt text Chartula sends, without the facts
/// (see <c>ChangelogPromptBuilder.PromptHash</c>).
/// Two files with the same hash were rendered from the same instructions.
/// </param>
/// <param name="Thinking">
/// The configured thinking mode. It records the setting, not whether the model thought:
/// <c>provider-default</c> leaves it to the model, and some models think by default
/// while others do not.
/// </param>
/// <param name="ThoroughCheck">Whether the thorough faithfulness check ran.</param>
/// <param name="FactBaseDepth">How much of each pull request the facts were built from.</param>
/// <param name="CheckModel">The model the thorough check asked, when it ran.</param>
/// <param name="CheckThinking">The thinking mode the thorough check asked for, when it ran.</param>
public sealed record RunProvenance(
    string? ToolVersion,
    string? Provider,
    string? Model,
    string? PromptHash,
    string? Thinking = null,
    bool? ThoroughCheck = null,
    string? FactBaseDepth = null,
    string? CheckModel = null,
    string? CheckThinking = null);
