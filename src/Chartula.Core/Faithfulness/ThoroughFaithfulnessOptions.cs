namespace Chartula.Core.Faithfulness;

/// <summary>
/// The single toggle for the thorough faithfulness check, the second LLM pass.
/// On by default. A maintainer can turn it off to save the extra LLM call.
/// </summary>
/// <param name="Enabled">Whether the second LLM pass runs. Defaults to on.</param>
public sealed record ThoroughFaithfulnessOptions(bool Enabled = true);
