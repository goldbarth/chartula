namespace Chartula.Core.Facts;

/// <summary>
/// The established facts behind a changelog entry that the LLM is allowed to
/// rephrase. An LLM only ever rephrases these; it never decides what happened.
/// </summary>
/// <remarks>
/// This is a minimal carrier. The real, structured fact base (per-PR facts,
/// labels, links) is built in its own issue and will enrich this type. It lives
/// here now only so the LLM seam has a typed input to depend on.
/// </remarks>
/// <param name="Statements">
/// Individual, self-contained fact statements drawn from the pull requests.
/// </param>
public sealed record GroundedFacts(IReadOnlyList<string> Statements);
