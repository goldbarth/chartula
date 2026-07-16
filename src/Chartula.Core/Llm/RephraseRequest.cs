using Chartula.Core.Facts;

namespace Chartula.Core.Llm;

/// <summary>
/// A request to rephrase grounded facts into audience-tailored prose.
/// </summary>
/// <param name="Facts">The established facts the output must stay true to.</param>
/// <param name="Audience">The audience the wording is tailored for.</param>
public sealed record RephraseRequest(GroundedFacts Facts, Audience Audience);
