using Chartula.Core.Facts;

namespace Chartula.Core.Llm;

/// <summary>
/// A request to check whether generated output is fully backed by the facts.
/// </summary>
/// <param name="Output">The generated changelog text to verify.</param>
/// <param name="Facts">The facts the output must be grounded in.</param>
public sealed record FaithfulnessRequest(string Output, GroundedFacts Facts);
