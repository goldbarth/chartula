namespace Chartula.Core.Faithfulness;

/// <summary>
/// The single toggle for the thorough (second-pass LLM) faithfulness check. On by
/// default; a maintainer can disable it to skip the extra LLM call.
/// </summary>
/// <param name="Enabled">Whether the second LLM pass runs. Defaults to on.</param>
public sealed record ThoroughFaithfulnessOptions(bool Enabled = true);
