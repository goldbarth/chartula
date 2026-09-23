namespace Chartula.Core.Observability;

/// <summary>What an LLM call was made for. Token cost is attributed per operation,
/// so the cost of the thorough faithfulness check is visible on its own.</summary>
public enum LlmOperation
{
    /// <summary>Rephrasing the facts into a changelog for one audience.</summary>
    Rephrase,

    /// <summary>The thorough (second-pass) faithfulness check.</summary>
    FaithfulnessCheck,
}
