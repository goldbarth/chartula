using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// A deterministic, LLM-free faithfulness check that catches obvious
/// hallucinations for free: numbers or quoted names in the output that the fact
/// base does not support. It always runs, by default.
/// </summary>
public interface IRuleBasedFaithfulnessChecker
{
    FaithfulnessReport Check(string output, FactBase factBase);
}
