namespace Chartula.Core.Llm;

/// <summary>
/// The result of a faithfulness check: whether the generated output is fully
/// backed by the facts, and any claims that are not.
/// </summary>
/// <param name="IsFaithful">
/// True when every claim in the output is supported by the facts.
/// </param>
/// <param name="UnsupportedClaims">
/// Claims found in the output that the facts do not back. Empty when faithful.
/// </param>
public sealed record FaithfulnessReport(
    bool IsFaithful,
    IReadOnlyList<string> UnsupportedClaims);
