namespace Chartula.Core.Observability;

/// <summary>What an LLM call was made for. Token cost is attributed per operation,
/// so the price of the thorough faithfulness check can be read on its own.</summary>
public enum LlmOperation
{
    /// <summary>Rephrasing the facts into a changelog for one audience.</summary>
    Rephrase,

    /// <summary>The thorough (second-pass) faithfulness check.</summary>
    FaithfulnessCheck,
}
