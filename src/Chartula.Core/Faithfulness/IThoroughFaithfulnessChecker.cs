using Chartula.Core.Facts;
using Chartula.Core.Llm;

namespace Chartula.Core.Faithfulness;

/// <summary>
/// A second, semantic faithfulness pass. It asks the LLM to check the generated output
/// against the fact base and to flag distortions of meaning the rule-based check cannot see.
/// On by default. <see cref="ThoroughFaithfulnessOptions.Enabled"/> turns it off, and
/// then no LLM call is made.
/// </summary>
public interface IThoroughFaithfulnessChecker
{
    Task<FaithfulnessReport> CheckAsync(
        string output,
        FactBase factBase,
        CancellationToken cancellationToken = default);
}
