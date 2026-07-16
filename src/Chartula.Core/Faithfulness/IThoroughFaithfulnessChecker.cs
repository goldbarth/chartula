using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// A second, semantic faithfulness pass: it asks the LLM to check the generated
/// output against the fact base and flag meaning-level distortions the rule-based
/// check cannot see. On by default; a single toggle disables it, in which case no
/// LLM call is made.
/// </summary>
public interface IThoroughFaithfulnessChecker
{
    Task<FaithfulnessReport> CheckAsync(
        string output,
        FactBase factBase,
        CancellationToken cancellationToken = default);
}
