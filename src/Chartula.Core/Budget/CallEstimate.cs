using Chartula.Core.Llm;
using Chartula.Core.Observability;

namespace Chartula.Core.Budget;

/// <summary>
/// The most one model call can consume. Both numbers are ceilings, not predictions:
/// a run that stays under them is guaranteed, one that is priced by them is not
/// what the bill will say.
/// </summary>
/// <param name="Operation">What the call is for.</param>
/// <param name="Audience">The audience the call renders or checks.</param>
/// <param name="MaxInputTokens">The most input tokens the call can be billed for.</param>
/// <param name="MaxOutputTokens">The most output tokens, thinking included - the provider's hard cap.</param>
public sealed record CallEstimate(
    LlmOperation Operation,
    Audience Audience,
    long MaxInputTokens,
    long MaxOutputTokens);
